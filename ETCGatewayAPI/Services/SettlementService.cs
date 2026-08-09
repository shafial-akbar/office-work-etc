using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using ETCGatewayAPI.Data;
using Microsoft.EntityFrameworkCore;
using Etc.Shared.Constants;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Etc.Shared.Helpers;

namespace ETCGatewayAPI.Services
{
    public class SettlementService : ISettlementService
    {
        private readonly DatabaseContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SettlementService> _logger;

        public SettlementService(DatabaseContext context,
                               IHttpContextAccessor httpContextAccessor,
                               ILogger<SettlementService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<SettlementReportResponse> GetSettlementReportAsync(SettlementReportRequest request)
        {
            try
            {
                // ১. বেস কুয়েরি: Success স্ট্যাটাস এবং BankTxnDate ফিল্টারিং
                var query = _context.DoTransactions
                    .AsNoTracking()
                    .Where(t => t.TranStatus == TranStatus.Success
                             && t.BankTxnDate >= request.FromDate
                             && t.BankTxnDate <= request.ToDate);

                // ২. TranMode ফিল্টার (Debit/Credit)
                if (!string.IsNullOrWhiteSpace(request.TranMode))
                {
                    query = query.Where(t => t.TranMode.Equals(request.TranMode, StringComparison.OrdinalIgnoreCase));
                }

                // ৩. SourceChannel ফিল্টার: শুধুমাত্র Credit Mode এর জন্য এবং যদি রিকোয়েস্টে পাঠানো হয়
                bool isCreditMode = !string.IsNullOrWhiteSpace(request.TranMode) &&
                                     request.TranMode.Equals(TranMode.Credit, StringComparison.OrdinalIgnoreCase);

                if (isCreditMode && !string.IsNullOrWhiteSpace(request.SourceChannel))
                {
                    query = query.Where(t => t.SourceChannel.Equals(request.SourceChannel, StringComparison.OrdinalIgnoreCase));
                }

                // ৪. TranMode অনুযায়ী Conditional Mapping (Credit vs Debit)
                var details = await query
                    .OrderByDescending(t => t.BankTxnDate)
                    .Select(t => new SettlementDetailDto
                    {
                        Id = t.Id,
                        BankTxnId = t.BankTxnId,
                        BankTxnDate = t.BankTxnDate,
                        SourceAccountNo = t.SourceAccountNo,
                        TransactionAmount = t.TransactionAmount,
                        TranMode = t.TranMode,
                        SourceChannel = t.SourceChannel,
                        SettlStatus = t.SettlStatus,

                        // Debit Mode হলে PartnerId ও PartnerTxnId আনবে, নাহলে null
                        PartnerId = t.TranMode.Equals(TranMode.Debit, StringComparison.OrdinalIgnoreCase) ? t.PartnerId : null,
                        PartnerTxnId = t.TranMode.Equals(TranMode.Debit, StringComparison.OrdinalIgnoreCase) ? t.PartnerTxnId : null,

                        // Credit Mode হলে RefNo1 ও ChannelTransactionDate আনবে, নাহলে null
                        RefNo1 = t.TranMode.Equals(TranMode.Credit, StringComparison.OrdinalIgnoreCase) ? t.RefNo1 : null,
                        ChannelTransactionDate = t.TranMode.Equals(TranMode.Credit, StringComparison.OrdinalIgnoreCase) ? t.ChannelTransactionDate : null
                    })
                    .ToListAsync();

                // ৫. Summary হিসাব করা
                var summary = new SettlementSummaryDto
                {
                    TotalTransactions = details.Count,
                    TotalAmount = details.Sum(d => d.TransactionAmount),
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    FilteredTranMode = !string.IsNullOrWhiteSpace(request.TranMode) ? request.TranMode.ToUpper() : "ALL"
                };

                return new SettlementReportResponse
                {
                    Summary = summary,
                    Details = details
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating settlement report for Date Range: {FromDate} to {ToDate}", request.FromDate, request.ToDate);
                throw;
            }
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

                // ৪. SettlementOperation অনুযায়ী TranMode নির্ধারণ ও Pending DoTransactions ফিল্টারিং
                string targetTranMode = request.SettlementOperation.Equals(SettlementOperation.Toll, StringComparison.OrdinalIgnoreCase)
                    ? TranMode.Debit
                    : TranMode.Credit;

                var transactionsToProcess = await _context.DoTransactions
                    .Where(t => t.BankTxnDate.Date == targetBankTxnDate.Date
                             && t.TranMode.Equals(targetTranMode, StringComparison.OrdinalIgnoreCase)
                             && t.TranStatus == TranStatus.Success
                             && t.SettlStatus == SettlementStatus.Pending)
                    .ToListAsync();

                if (transactionsToProcess == null || !transactionsToProcess.Any())
                {
                    return new DataprocessResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = $"No pending {request.SettlementOperation} transactions found for Process on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }

                // ৬. DoTransactions টেবিলে BatchProcessId এবং SettlStatus আপডেট
                foreach (var txn in transactionsToProcess)
                {
                    txn.BatchProcessId = batchProcessId;
                    txn.SettlStatus = SettlementStatus.Processing;
                }

                // ৭. Settlement টেবিলে নতুন এন্ট্রি তৈরি করা
                var settlementRecord = new Settlement
                {
                    Id = Guid.NewGuid(),
                    BankTxnDate = targetBankTxnDate,
                    BatchProcessId = batchProcessId,
                    TotalAmount = transactionsToProcess.Sum(x => x.TransactionAmount),
                    TxnCount = transactionsToProcess.Count,
                    BankAccountNo = string.Empty,
                    Status = SettlementStatus.Processing,
                    ProcessBrCode = request.BrCode,
                    ProcessedBy = request.UserId,
                    SettlementOperation = request.SettlementOperation,
                    ProcessedAt = DateTime.UtcNow,
                };

                await _context.Settlements.AddAsync(settlementRecord);

                // ৮. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return new DataprocessResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = $"Processing completed successfully for {request.SettlementOperation}. BatchProcessId: {batchProcessId}, Total Amount: {transactionsToProcess.Count}"
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

                // ৩. SettlementOperation অনুযায়ী TranMode নির্ধারণ ও Processing ট্রানজেকশন ফিল্টারিং
                string targetTranMode = request.SettlementOperation.Equals(SettlementOperation.Toll, StringComparison.OrdinalIgnoreCase)
                    ? TranMode.Debit
                    : TranMode.Credit;

                var transactionsToSettle = await _context.DoTransactions
                    .Where(t => t.BankTxnDate.Date == targetBankTxnDate.Date
                             && t.TranMode.Equals(targetTranMode, StringComparison.OrdinalIgnoreCase)
                             && t.TranStatus == TranStatus.Success
                             && t.SettlStatus == SettlementStatus.Processing)
                    .ToListAsync();

                if (transactionsToSettle == null || !transactionsToSettle.Any())
                {
                    return new SettlementResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = $"No Processing {request.SettlementOperation} transactions found for Settle on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }

                // ৪. প্রথম ট্রানজেকশনের BatchProcessId দিয়ে Settlement রেকর্ড খোঁজা
                string batchProcessId = transactionsToSettle[0].BatchProcessId;
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

                // ৫. Eihher Toll Operation প্রসেসিং
                if (request.SettlementOperation.Equals(SettlementOperation.Toll, StringComparison.OrdinalIgnoreCase))
                {
                    // CBS Integration
                    var cbsResult = await SendToCbsAsync(settleRecord);

                    // TODO: cbsResult.IsSuccess চেক যুক্ত করুন
                    if (cbsResult.IsSuccess)
                    {
                        // Settlements টেবিল আপডেট
                        settleRecord.CBSRef = ""; // cbsResult থেকে প্রাপ্ত রেফারেন্স আইডি
                        settleRecord.BankAccountNo = "";
                        settleRecord.CBSResponse = "";

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
                }
                // ৬. Or TopUp Operation প্রসেসিং
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

                // ৭. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট (সঠিক স্থানে রয়েছে)
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return new SettlementResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = $"Settlement completed successfully for {request.SettlementOperation}, Total Amount: {settleRecord.TotalAmount}"
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

        /// <summary>
        /// CBS (Core Banking System) এপিআই-তে সেটেলমেন্ট রিকোয়েস্ট পাঠানোর প্লেসহোল্ডার মেথড।
        /// আসল সিবিএস ইন্টিগ্রেশনের সময় এখানে সিবিএস সার্ভিস কল এবং রেসপন্স পার্সিং যোগ করতে হবে।
        /// </summary>
        private async Task<CbsResponseModel> SendToCbsAsync(Settlement settleRecord)
        {
            try
            {
                _logger.LogInformation("Initiating CBS Settlement call. BatchProcessId: {BatchId}, Amount: {Amount}",
                    settleRecord.BatchProcessId, settleRecord.TotalAmount);

                // TODO: আসল CBS API ইন্টিগ্রেশন কোড এখানে যুক্ত হবে
                // উদাহরণ: var cbsResult = await _cbsApiClient.PostSettlementAsync(payload);

                await Task.Delay(100); // অ্যাসিনক্রোনাস কলের সিমুলেশন

                // মক সফল রেসপন্স (CBS ইন্টিগ্রেশন সম্পন্ন হলে এটি ডায়নামিক হবে)
                return new CbsResponseModel
                {
                    IsSuccess = true,
                    CbsRef = $"CBS{DateTime.UtcNow:yyyyMMddHHmmss}",
                    BankAccountNo = "1001002003004", // নির্দিষ্ট সিস্টেম অ্যাকাউন্ট নম্বর
                    ResponseMessage = "Successfully posted to CBS"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CBS Service Call Failed for BatchProcessId: {BatchId}", settleRecord.BatchProcessId);

                return new CbsResponseModel
                {
                    IsSuccess = false,
                    CbsRef = string.Empty,
                    BankAccountNo = string.Empty,
                    ResponseMessage = $"CBS Connection Error: {ex.Message}"
                };
            }
        }

        // CBS রেসপন্স মডেল
        public class CbsResponseModel
        {
            public bool IsSuccess { get; set; }
            public string CbsRef { get; set; } = string.Empty;
            public string BankAccountNo { get; set; } = string.Empty;
            public string ResponseMessage { get; set; } = string.Empty;
        }
    }
}