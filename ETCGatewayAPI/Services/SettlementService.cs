using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using ETCGatewayAPI.Data;
using Microsoft.EntityFrameworkCore;
using Etc.Shared.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Etc.Shared.Helpers;
using System.Diagnostics;
using Newtonsoft.Json;

namespace ETCGatewayAPI.Services
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
                // ১. রিকোয়েস্টের BankTxnDate পার্স করা
                DateTime targetBankTxnDate = DateTimeHelper.ParseToDateTime(request.BankTxnDate);

                // ২. প্রারম্ভিক চেক: উক্ত তারিখ ও অপারেশনের জন্য ইতোমধ্যে Settled বা Processing আছে কিনা
                bool isAlreadyProcessedOrSettled = await _context.Settlements
                    .AnyAsync(s => s.BankTxnDate.Date == targetBankTxnDate.Date
                                && s.SettlementOperation.Equals(request.SettlementOperation, StringComparison.OrdinalIgnoreCase)
                                && (s.Status == SettlementStatus.Settled || s.Status == SettlementStatus.Processing));

                if (isAlreadyProcessedOrSettled)
                {
                    return new DataprocessResponse
                    {
                        HttpCode = 409,
                        HttpStatus = "Conflict",
                        Message = $"Data processing has already been completed or is currently in Processing state for {request.SettlementOperation} on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }

                // ৩. ইউনিক BatchProcessId তৈরি করা
                string batchProcessId = $"{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}";

                List<DoTransaction> transactionsToProcess = new();

                decimal totalSuccessAmount = 0m;
                decimal totalReverseAmount = 0m;
                decimal grossTotalAmount = 0m;
                decimal netSettlementAmount = 0m;

                int successCount = 0;
                int reverseCount = 0;
                int netCount = 0;

                // ৪. SettlementOperation অনুযায়ী ডায়নামিক ফিল্টারিং ও নিট ক্যালকুলেশন
                if (request.SettlementOperation.Equals(SettlementOperation.Toll, StringComparison.OrdinalIgnoreCase))
                {
                    // TOLL: Success Debit এবং Reversed Transactions উভয়ই প্রসেস হবে
                    transactionsToProcess = await _context.DoTransactions
                        .Where(t => t.BankTxnDate.Date == targetBankTxnDate.Date
                                 && t.SettlStatus == SettlementStatus.Pending
                                 && (t.TranStatus == TranStatus.Success || t.TranStatus == TranStatus.Reversed))
                        .ToListAsync();

                    if (!transactionsToProcess.Any())
                    {
                        return new DataprocessResponse
                        {
                            HttpCode = 404,
                            HttpStatus = "Not Found",
                            Message = $"No pending Toll transactions found for Process on {targetBankTxnDate:yyyy-MM-dd}."
                        };
                    }

                    var successTxns = transactionsToProcess
                        .Where(t => t.TranStatus == TranStatus.Success && t.TranMode.Equals(TranMode.Debit, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var reversedTxns = transactionsToProcess
                        .Where(t => t.TranStatus == TranStatus.Reversed)
                        .ToList();

                    totalSuccessAmount = successTxns.Sum(x => x.TransactionAmount);
                    totalReverseAmount = reversedTxns.Sum(x => x.TransactionAmount);

                    // Amount Calculations
                    grossTotalAmount = totalSuccessAmount + totalReverseAmount;    // Success Amount + Reverse Amount
                    netSettlementAmount = totalSuccessAmount - totalReverseAmount; // Success Amount - Reverse Amount

                    // Count Calculations
                    successCount = successTxns.Count;
                    reverseCount = reversedTxns.Count;
                    netCount = successCount - reverseCount;                        // Success Count - Reverse Count
                }
                else
                {
                    // TOPUP: শুধুমাত্র সফল Credit Transaction-গুলো প্রসেস হবে
                    transactionsToProcess = await _context.DoTransactions
                        .Where(t => t.BankTxnDate.Date == targetBankTxnDate.Date
                                 && t.TranMode.Equals(TranMode.Credit, StringComparison.OrdinalIgnoreCase)
                                 && t.TranStatus == TranStatus.Success
                                 && t.SettlStatus == SettlementStatus.Pending)
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

                // ৫. DoTransactions টেবিলে BatchProcessId এবং SettlStatus আপডেট
                foreach (var txn in transactionsToProcess)
                {
                    txn.BatchProcessId = batchProcessId;
                    txn.SettlStatus = SettlementStatus.Processing;
                }

                // ৬. Settlement টেবিলে নতুন ফিল্ড স্ট্রাকচারসহ এন্ট্রি তৈরি
                var settlementRecord = new Settlement
                {
                    Id = Guid.NewGuid(),
                    BankTxnDate = targetBankTxnDate,
                    BatchProcessId = batchProcessId,

                    // Amount Properties
                    TotalAmount = grossTotalAmount,           // Success Amount + Reverse Amount
                    ReverseAmount = totalReverseAmount,       // Total Reverse Amount
                    NetSettlementAmount = netSettlementAmount, // Success Amount - Reverse Amount

                    // Count Properties
                    TotalCount = transactionsToProcess.Count, // Total Count (Success + Reverse)
                    ReverseCount = reverseCount,              // Reverse Count
                    NetCount = netCount,                      // Net Count (Success - Reverse)

                    SettlementAccountNo = _configuration["PostTransaction:ETCSettleAcct"]!,
                    Status = SettlementStatus.Processing,
                    ProcessBrCode = request.BrCode,
                    ProcessedBy = request.UserId,
                    SettlementOperation = request.SettlementOperation,
                    ProcessedAt = DateTime.UtcNow,
                };

                await _context.Settlements.AddAsync(settlementRecord);

                // ৭. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return new DataprocessResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = $"Processing completed successfully for {request.SettlementOperation}. BatchProcessId: {batchProcessId}, Total Txns: {transactionsToProcess.Count}, Net Amount: {netSettlementAmount}"
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
                // ১. রিকোয়েস্টের BankTxnDate পার্স করা
                DateTime targetBankTxnDate = DateTimeHelper.ParseToDateTime(request.BankTxnDate);

                // ২. প্রারম্ভিক চেক: উক্ত তারিখ ও অপারেশনের জন্য ইতোমধ্যে Settled করা হয়েছে কিনা
                bool isAlreadySettled = await _context.Settlements
                    .AnyAsync(s => s.BankTxnDate.Date == targetBankTxnDate.Date
                                && s.SettlementOperation.Equals(request.SettlementOperation, StringComparison.OrdinalIgnoreCase)
                                && s.Status == SettlementStatus.Settled);

                if (isAlreadySettled)
                {
                    return new SettlementResponse
                    {
                        HttpCode = 409,
                        HttpStatus = "Conflict",
                        Message = $"Settlement has already been completed for {request.SettlementOperation} on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }

                // ৩. SettlementOperation অনুযায়ী DoTransactions ফিল্টারিং
                List<DoTransaction> transactionsToSettle = new();

                if (request.SettlementOperation.Equals(SettlementOperation.Toll, StringComparison.OrdinalIgnoreCase))
                {
                    // TOLL: Processing অবস্থায় থাকা Success এবং Reversed ট্রানজেকশনগুলো সিলেক্ট করা
                    transactionsToSettle = await _context.DoTransactions
                        .Where(t => t.BankTxnDate.Date == targetBankTxnDate.Date
                                 && t.SettlStatus == SettlementStatus.Processing
                                 && (t.TranStatus == TranStatus.Success || t.TranStatus == TranStatus.Reversed))
                        .ToListAsync();
                }
                else
                {
                    // TOPUP: Processing অবস্থায় থাকা Credit Success ট্রানজেকশনগুলো সিলেক্ট করা
                    transactionsToSettle = await _context.DoTransactions
                        .Where(t => t.BankTxnDate.Date == targetBankTxnDate.Date
                                 && t.TranMode.Equals(TranMode.Credit, StringComparison.OrdinalIgnoreCase)
                                 && t.TranStatus == TranStatus.Success
                                 && t.SettlStatus == SettlementStatus.Processing)
                        .ToListAsync();
                }

                if (transactionsToSettle == null || !transactionsToSettle.Any())
                {
                    return new SettlementResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = $"No Processing {request.SettlementOperation} transactions found for Settle on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }

                // ৪. প্রথম ট্রানজেকশনের BatchProcessId দিয়ে Settlement রেকর্ড খোঁজা
                string batchProcessId = transactionsToSettle[0].BatchProcessId!;
                var settleRecord = await _context.Settlements
                    .FirstOrDefaultAsync(t => t.BatchProcessId == batchProcessId);

                if (settleRecord == null)
                {
                    return new SettlementResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = $"No settlement batch record found for BatchProcessId: {batchProcessId}."
                    };
                }

                // ৫. Toll Operation প্রসেসিং (CBS Voucher Posting)
                if (request.SettlementOperation.Equals(SettlementOperation.Toll, StringComparison.OrdinalIgnoreCase))
                {
                    // CBS Call (Request-এর BrCode এবং SettleRecord-এর NetSettlementAmount পাঠানো হচ্ছে)
                    var cbsResponse = await SendToCbsAsync(settleRecord.NetSettlementAmount, request.BrCode, settleRecord.BatchProcessId);

                    if (cbsResponse.Status == "200" || cbsResponse.Status == "1004")
                    {
                        // Settlements টেবিল আপডেট
                        settleRecord.CBSResponse = JsonConvert.SerializeObject(cbsResponse);
                        settleRecord.SettledBy = request.UserId;
                        settleRecord.SettleBrCode = request.BrCode;
                        settleRecord.SettledAt = DateTime.UtcNow;
                        settleRecord.Status = SettlementStatus.Settled;

                        // DoTransactions টেবিল আপডেট (Success + Reversed উভয় ফিল্টার হওয়া আইটেম Settled হবে)
                        foreach (var txn in transactionsToSettle)
                        {
                            txn.SettlStatus = SettlementStatus.Settled;
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
                // ৬. TopUp Operation প্রসেসিং
                else
                {
                    // Settlements টেবিল আপডেট
                    settleRecord.SettledBy = request.UserId;
                    settleRecord.SettleBrCode = request.BrCode;
                    settleRecord.SettledAt = DateTime.UtcNow;
                    settleRecord.Status = SettlementStatus.Settled;

                    // DoTransactions টেবিল আপডেট
                    foreach (var txn in transactionsToSettle)
                    {
                        txn.SettlStatus = SettlementStatus.Settled;
                    }
                }

                // ৭. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return new SettlementResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = $"Settlement completed successfully for {request.SettlementOperation}. BatchProcessId: {batchProcessId}, Net Settlement Amount: {settleRecord.NetSettlementAmount}"
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

        public async Task<PostTransactionResponse> SendToCbsAsync(decimal netSettlementAmount, string settleBrCode, string batchProcessId)
        {
            var postTransactionResponse = new PostTransactionResponse();
            string batchDateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string narration = $"{batchProcessId}|{settleBrCode}|{batchDateStr}";

            var postTransactionRequest = new PostTransactionRequest()
            {
                UserName = _configuration["PostTransaction:UserName"]!,
                ServiceCode = _configuration["PostTransaction:ServiceCode"]!,
                SpCode = _configuration["PostTransaction:SpCode"]!,
                ChannelId = _configuration["PostTransaction:ChannelId"]!,
                ReferenceNo = batchProcessId,
                ReferenceDate = batchDateStr,
                OrigBrnCode = settleBrCode,
                BatchNarration = batchProcessId,
                Debits = new List<Debit>(),
                Credits = new List<Credit>()
            };

            if (netSettlementAmount > 0)
            {
                // Debit Portion (GL Account)
                postTransactionRequest.Debits.Add(new Debit()
                {
                    GlAccCode = _configuration["PostTransaction:ETCParkingGL"]!,
                    GlBrnCode = settleBrCode,
                    Amount = netSettlementAmount,
                    Narration = narration,
                    CreditNarration = narration
                });

                // Credit Portion (Settlement Account)
                postTransactionRequest.Credits.Add(new Credit()
                {
                    AccountNumber = _configuration["PostTransaction:ETCSettleAcct"]!,
                    Amount = netSettlementAmount,
                    Narration = narration,
                    DebitNarration = narration
                });
            }

            try
            {
                string baseUrl = _configuration["PostTransaction:BaseUrl"]!;
                string requestUrl = $"{baseUrl.TrimEnd('/')}/PostTransaction";
                _logger.LogInformation("SendToCbsAsync Request: {Request}", JsonConvert.SerializeObject(postTransactionRequest));

                // Inject করা _httpClient ব্যবহার করা হয়েছে
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