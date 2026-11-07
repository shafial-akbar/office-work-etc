using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using ETCAdminUI.Data;
using Microsoft.EntityFrameworkCore;
using Etc.Shared.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Etc.Shared.Helpers;
using System.Diagnostics;
using Newtonsoft.Json;

namespace ETCAdminUI.Services
{
    public class SettlementService : ISettlementService
    {
        private readonly DatabaseContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SettlementService> _logger;

        public SettlementService(DatabaseContext context, IConfiguration configuration, 
                               IHttpContextAccessor httpContextAccessor,
                               ILogger<SettlementService> logger, HttpClient httpClient)
        {
            _context = context;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<DataprocessResponse> DoDataprocessAsync(DataprocessRequest request)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ১. রিকোয়েস্টের BankTxnDate পার্স করা এবং ইনডেক্স-ফ্রেন্ডলি ডেট রেঞ্জ তৈরি
                DateTime targetBankTxnDate = DateTimeHelper.ParseToDateTime(request.BankTxnDate);
                DateTime startDate = targetBankTxnDate.Date;
                DateTime endDate = startDate.AddDays(1);

                string operation = request.SettlementOperation.Trim();
                bool isTollOperation = operation.Equals(SettlementOperation.Toll, StringComparison.OrdinalIgnoreCase);

                // ২. প্রারম্ভিক চেক: উক্ত তারিখ ও অপারেশনের জন্য ইতোমধ্যে Settled বা Processing আছে কিনা
                bool isAlreadyProcessedOrSettled = await _context.Settlements
                    .AnyAsync(s => s.BankTxnDate >= startDate && s.BankTxnDate < endDate
                                && s.SettlementOperation.ToLower() == operation.ToLower()
                                && (s.Status == SettlementStatus.Settled || s.Status == SettlementStatus.Processing));

                if (isAlreadyProcessedOrSettled)
                {
                    return new DataprocessResponse
                    {
                        HttpCode = 409,
                        HttpStatus = "Conflict",
                        Message = $"Data processing has already been completed or is currently in Processing state for {operation} on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }

                // ৩. ইউনিক BatchProcessId তৈরি
                string batchProcessId = $"{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}";

                List<DoTransaction> transactionsToProcess = new();

                decimal totalSuccessAmount = 0m;
                decimal totalReverseAmount = 0m;
                decimal grossTotalAmount = 0m;
                decimal netSettlementAmount = 0m;

                int successCount = 0;
                int reverseCount = 0;
                int netCount = 0;

                // ৪. SettlementOperation অনুযায়ী পৃথক ও নির্ভুল ক্যালকুলেশন
                if (isTollOperation)
                {
                    // TOLL ৪.১: শুধুমাত্র বৈধ সফল ডেবিট ট্রানজেকশনগুলো আনা (যা e-Chalan এ সেটেল হবে)
                    var validTollTxns = await _context.DoTransactions
                        .Where(t => t.BankTxnDate >= startDate && t.BankTxnDate < endDate
                                 && t.TranMode == TranMode.Debit
                                 && t.TranStatus == TranStatus.Success
                                 && t.SettlStatus == SettlementStatus.Pending)
                        .ToListAsync();

                    if (!validTollTxns.Any())
                    {
                        return new DataprocessResponse
                        {
                            HttpCode = 404,
                            HttpStatus = "Not Found",
                            Message = $"No pending Toll transactions found for Process on {targetBankTxnDate:yyyy-MM-dd}."
                        };
                    }

                    // TOLL ৪.২: অডিট ও রিপোর্টিংয়ের জন্য উক্ত দিনের রিভার্সড ট্রানজেকশনগুলো আনা
                    var reversedTxns = await _context.DoTransactions
                        .Where(t => t.BankTxnDate >= startDate && t.BankTxnDate < endDate
                                 && t.TranMode == TranMode.Debit
                                 && (t.TranStatus == TranStatus.Reversed || t.SettlStatus == SettlementStatus.Reversed))
                        .ToListAsync();

                    // TOLL ৪.৩: নির্ভুল একাউন্টিং ক্যালকুলেশন (ডাবল ডিডাকশন মুক্ত)
                    // validTollTxns এর যোগফলই হলো আসল নিট টাকা যা সিবিএসে e-Chalan এ যাবে
                    netSettlementAmount = validTollTxns.Sum(x => x.TransactionAmount);
                    totalSuccessAmount = netSettlementAmount;
                    totalReverseAmount = reversedTxns.Sum(x => x.TransactionAmount);

                    // গ্রস অ্যামাউন্ট = সফল টোল + রিভার্সড টোল
                    grossTotalAmount = netSettlementAmount + totalReverseAmount;

                    netCount = validTollTxns.Count;
                    reverseCount = reversedTxns.Count;
                    successCount = netCount;

                    // বৈধ ট্রানজেকশনগুলোকে প্রসেসিং তালিকার অন্তর্ভুক্ত করা
                    transactionsToProcess = validTollTxns;

                    // রিভার্সড রেকর্ডগুলোতে অডিট ট্র্যাকিংয়ের জন্য BatchProcessId বসানো (SettlStatus=Reversed অপরিবর্তিত থাকবে)
                    foreach (var revTxn in reversedTxns)
                    {
                        revTxn.BatchProcessId = batchProcessId;
                    }

                    // ইন-হাউস রিফান্ড ক্রেডিট (_REV) যদি Pending থাকে, তবে সেগুলোকে Settled করে রাখা
                    var pendingRevCredits = await _context.DoTransactions
                        .Where(t => t.BankTxnDate >= startDate && t.BankTxnDate < endDate
                                 && t.TranMode == TranMode.Credit
                                 && t.SettlStatus == SettlementStatus.Pending
                                 && (!string.IsNullOrEmpty(t.OriginalBankTxnId) || (t.PartnerTxnId != null && t.PartnerTxnId.EndsWith("_REV"))))
                        .ToListAsync();

                    foreach (var revCredit in pendingRevCredits)
                    {
                        revCredit.BatchProcessId = batchProcessId;
                        revCredit.SettlStatus = SettlementStatus.Settled;
                    }
                }
                else
                {
                    // TOPUP ৪.১: শুধুমাত্র জেনুইন কাস্টমার টপ-আপ ক্রেডিট আনা (রিভার্সাল রিফান্ড ক্রেডিট সম্পূর্ণ বাদ)
                    transactionsToProcess = await _context.DoTransactions
                        .Where(t => t.BankTxnDate >= startDate && t.BankTxnDate < endDate
                                 && t.TranMode == TranMode.Credit
                                 && t.TranStatus == TranStatus.Success
                                 && t.SettlStatus == SettlementStatus.Pending
                                 && string.IsNullOrEmpty(t.OriginalBankTxnId) // Reversal Credit বাদ
                                 && (t.PartnerTxnId == null || !t.PartnerTxnId.EndsWith("_REV")))
                        .ToListAsync();

                    if (!transactionsToProcess.Any())
                    {
                        return new DataprocessResponse
                        {
                            HttpCode = 404,
                            HttpStatus = "Not Found",
                            Message = $"No pending TopUp transactions found for Process on {targetBankTxnDate:yyyy-MM-dd}."
                        };
                    }

                    totalSuccessAmount = transactionsToProcess.Sum(x => x.TransactionAmount);
                    totalReverseAmount = 0m;

                    grossTotalAmount = totalSuccessAmount;
                    netSettlementAmount = totalSuccessAmount;

                    successCount = transactionsToProcess.Count;
                    reverseCount = 0;
                    netCount = successCount;
                }

                // ৫. শুধুমাত্র সেটেলমেন্টের যোগ্য ট্রানজেকশনগুলোর SettlStatus -> Processing করা
                foreach (var txn in transactionsToProcess)
                {
                    txn.BatchProcessId = batchProcessId;
                    txn.SettlStatus = SettlementStatus.Processing;
                }

                // ৬. Settlement টেবিলে মাস্টার ব্যাচ সামারি সংরক্ষণ
                var settlementRecord = new Settlement
                {
                    Id = Guid.NewGuid(),
                    BankTxnDate = targetBankTxnDate,
                    BatchProcessId = batchProcessId,

                    // Amount Properties
                    TotalAmount = grossTotalAmount,           // গ্রস কালেকশন চেষ্টা (Success + Reverse)
                    ReverseAmount = totalReverseAmount,       // মোট রিভার্সড টোল
                    NetSettlementAmount = netSettlementAmount, // সিবিএসে e-Chalan এ যাওয়ার প্রকৃত টাকা

                    // Count Properties
                    TotalCount = netCount + reverseCount,     // সর্বমোট ট্রানজেকশন সংখ্যা
                    ReverseCount = reverseCount,              // রিভার্সাল সংখ্যা
                    NetCount = netCount,                      // প্রকৃত সফল ট্রানজেকশন সংখ্যা

                    SettlementAccountNo = _configuration["PostTransaction:ETCSettleAcct"] ?? string.Empty,
                    Status = SettlementStatus.Processing,
                    ProcessBrCode = request.BrCode,
                    ProcessedBy = request.UserId,
                    SettlementOperation = operation,
                    ProcessedAt = DateTime.UtcNow
                };

                await _context.Settlements.AddAsync(settlementRecord);

                // ৭. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation("Data processing completed successfully for {Operation}. Batch: {BatchId}, Net Amount: {Amount}",
                    operation, batchProcessId, netSettlementAmount);

                return new DataprocessResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = $"Processing completed successfully for {operation}. BatchProcessId: {batchProcessId}, Valid Txns: {transactionsToProcess.Count}, Net Amount: {netSettlementAmount}"
                };
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during Data Processing for Operation: {Operation}", request.SettlementOperation);

                return new DataprocessResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = $"An error occurred during Processing: {ex.Message}"
                };
            }
        }

        public async Task<SettlementResponse> DoSettlementAsync(SettlementRequest request)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ১. রিকোয়েস্টের BankTxnDate পার্স করা এবং ইনডেক্স-ফ্রেন্ডলি ডেট রেঞ্জ তৈরি
                DateTime targetBankTxnDate = DateTimeHelper.ParseToDateTime(request.BankTxnDate);
                DateTime startDate = targetBankTxnDate.Date;
                DateTime endDate = startDate.AddDays(1);

                string operation = request.SettlementOperation.Trim();
                bool isTollOperation = operation.Equals(SettlementOperation.Toll, StringComparison.OrdinalIgnoreCase);

                // ২. প্রারম্ভিক চেক: উক্ত তারিখ ও অপারেশনের জন্য ইতোমধ্যে Settled করা হয়েছে কিনা
                bool isAlreadySettled = await _context.Settlements
                    .AnyAsync(s => s.BankTxnDate >= startDate && s.BankTxnDate < endDate
                                && s.SettlementOperation.ToLower() == operation.ToLower()
                                && s.Status == SettlementStatus.Settled);

                if (isAlreadySettled)
                {
                    return new SettlementResponse
                    {
                        HttpCode = 409,
                        HttpStatus = "Conflict",
                        Message = $"Settlement has already been completed for {operation} on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }

                // ৩. Processing অবস্থায় থাকা Settlement ব্যাচ রেকর্ডটি খুঁজে বের করা
                var settleRecord = await _context.Settlements
                    .FirstOrDefaultAsync(s => s.BankTxnDate >= startDate && s.BankTxnDate < endDate
                                           && s.SettlementOperation.ToLower() == operation.ToLower()
                                           && s.Status == SettlementStatus.Processing);

                if (settleRecord == null)
                {
                    return new SettlementResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = $"No processing settlement batch found for {operation} on {targetBankTxnDate:yyyy-MM-dd}. Please run Data Process first."
                    };
                }

                string batchProcessId = settleRecord.BatchProcessId;

                // ৪. উক্ত ব্যাচের অন্তর্ভুক্ত প্রসেসিং ট্রানজেকশনগুলো নিয়ে আসা (শুধুমাত্র Success ট্রানজেকশন)
                var transactionsToSettle = await _context.DoTransactions
                    .Where(t => t.BatchProcessId == batchProcessId
                             && t.SettlStatus == SettlementStatus.Processing
                             && t.TranStatus == TranStatus.Success)
                    .ToListAsync();

                if (!transactionsToSettle.Any())
                {
                    return new SettlementResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = $"No valid processing transactions found for BatchProcessId: {batchProcessId}."
                    };
                }

                // ৫. Toll Operation প্রসেসিং (CBS Double-Entry Voucher Posting)
                if (isTollOperation)
                {
                    // CBS Call (Request-এর BrCode এবং SettleRecord-এর NetSettlementAmount পাঠানো হচ্ছে)
                    var cbsResponse = await SendToCbsAsync(new BatchRequestDto
                    {
                        NetSettlementAmount = settleRecord.NetSettlementAmount,
                        BrCode = request.BrCode,
                        BatchProcessId = settleRecord.BatchProcessId
                    });

                    if (cbsResponse.Status == "200" || cbsResponse.Status == "1004")
                    {
                        DateTime settledTime = DateTime.UtcNow;
                        DateTime localNow = DateTime.Now;

                        // Settlements মাস্টার টেবিল আপডেট
                        settleRecord.CBSResponse = JsonConvert.SerializeObject(cbsResponse);
                        settleRecord.SettledBy = request.UserId;
                        settleRecord.SettleBrCode = request.BrCode;
                        settleRecord.SettledAt = settledTime;
                        settleRecord.Status = SettlementStatus.Settled;

                        // শুধুমাত্র সফল ডেবিট ট্রানজেকশনগুলো Settled হবে
                        foreach (var txn in transactionsToSettle)
                        {
                            txn.SettlStatus = SettlementStatus.Settled;
                            txn.SettlDate = localNow;
                        }
                    }
                    else
                    {
                        return new SettlementResponse
                        {
                            HttpCode = 400,
                            HttpStatus = "Bad Request",
                            Message = $"SendToCBS Failed for BatchProcessId: {batchProcessId}. Response Code: {cbsResponse.Status}. Please try again."
                        };
                    }
                }
                // ৬. TopUp Operation প্রসেসিং (CBS Voucher ছাড়া সরাসরি Settled)
                else
                {
                    DateTime settledTime = DateTime.UtcNow;
                    DateTime localNow = DateTime.Now;

                    // Settlements মাস্টার টেবিল আপডেট
                    settleRecord.SettledBy = request.UserId;
                    settleRecord.SettleBrCode = request.BrCode;
                    settleRecord.SettledAt = settledTime;
                    settleRecord.Status = SettlementStatus.Settled;

                    // টপ-আপ ট্রানজেকশনগুলো Settled মার্ক করা
                    foreach (var txn in transactionsToSettle)
                    {
                        txn.SettlStatus = SettlementStatus.Settled;
                        txn.SettlDate = localNow;
                    }
                }

                // ৭. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation("Settlement completed successfully for {Operation}. Batch: {BatchId}, Amount: {Amount}",
                    operation, batchProcessId, settleRecord.NetSettlementAmount);

                return new SettlementResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = $"Settlement completed successfully for {operation}. BatchProcessId: {batchProcessId}, Net Settlement Amount: {settleRecord.NetSettlementAmount}"
                };
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during Settlement processing for Operation: {Operation}", request.SettlementOperation);

                return new SettlementResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = $"An error occurred during Settlement: {ex.Message}"
                };
            }
        }

        public async Task<PostTransactionResponse> SendToCbsAsync(BatchRequestDto request)
        {
            var postTransactionResponse = new PostTransactionResponse();
            string batchDateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string narration = $"{request.BatchProcessId}|{request.BrCode}|{batchDateStr}";

            var postTransactionRequest = new PostTransactionRequest()
            {
                UserName = _configuration["PostTransaction:UserName"]!,
                ServiceCode = _configuration["PostTransaction:ServiceCode"]!,
                SpCode = _configuration["PostTransaction:SpCode"]!,
                ChannelId = _configuration["PostTransaction:ChannelId"]!,
                ReferenceNo = request.BatchProcessId,
                ReferenceDate = batchDateStr,
                OrigBrnCode = request.BrCode,
                BatchNarration = request.BatchProcessId,
                Debits = new List<Debit>(),
                Credits = new List<Credit>()
            };

            if (request.NetSettlementAmount > 0)
            {
                // Debit Portion (GL Account)
                postTransactionRequest.Debits.Add(new Debit()
                {
                    GlAccCode = _configuration["PostTransaction:ETCParkingGL"]!,
                    GlBrnCode = request.BrCode,
                    Amount = request.NetSettlementAmount,
                    Narration = narration,
                    CreditNarration = narration
                });

                // Credit Portion (Settlement Account)
                postTransactionRequest.Credits.Add(new Credit()
                {
                    AccountNumber = _configuration["PostTransaction:ETCSettleAcct"]!,
                    Amount = request.NetSettlementAmount,
                    Narration = narration,
                    DebitNarration = narration
                });
            }

            try
            {
                string baseUrl = _configuration["PostTransaction:BaseUrl"]!;
                string requestUrl = $"{baseUrl.TrimEnd('/')}/PostTransaction";
                _logger.LogInformation("SendToCbsAsync Request: {Request}", JsonConvert.SerializeObject(postTransactionRequest));

                var response = await _httpClient.PostAsJsonAsync(requestUrl, postTransactionRequest);

                if (response.IsSuccessStatusCode)
                {
                    postTransactionResponse = await response.Content.ReadFromJsonAsync<PostTransactionResponse>()
                                              ?? new PostTransactionResponse();

                    _logger.LogInformation("SendToCbsAsync Result: {Result}", JsonConvert.SerializeObject(postTransactionResponse));
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("SendToCbsAsync HTTP Error {StatusCode}: {Error}", response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendToCbsAsync Exception occurred");
            }

            return postTransactionResponse;
        }
    }
}