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
                // ১. রিকোয়েস্টের BankTxnDate পার্স করা
                DateTime targetBankTxnDate = DateTimeHelper.ParseToDateTime(request.BankTxnDate);

                // ২. প্রারম্ভিক চেক: উক্ত তারিখ ও অপারেশনের জন্য ইতোমধ্যে সেটেলমেন্ট/Process করা হয়েছে কিনা
                bool isAlreadySettledOrPending = await _context.Settlements
                    .AnyAsync(s => s.BankTxnDate.Date == targetBankTxnDate.Date
                                && s.SettlementOperation.Equals(request.SettlementOperation, StringComparison.OrdinalIgnoreCase)
                                && (s.Status == SettlementStatus.Settled || s.Status == SettlementStatus.Pending));

                if (isAlreadySettledOrPending)
                {
                    return new DataprocessResponse
                    {
                        HttpCode = 409,
                        HttpStatus = "Conflict",
                        Message = $"Settlement has already been Settled or is currently in Process for {request.SettlementOperation} on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }

                // ৩. ইউনিক BatchProcessId তৈরি করা
                string batchProcessId = $"{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}";

                // ৪. SettlementOperation অনুযায়ী TranMode নির্ধারণ ও DoTransactions ফিল্টারিং
                string targetTranMode = request.SettlementOperation.Equals(SettlementOperation.Toll, StringComparison.OrdinalIgnoreCase)
                    ? TranMode.Debit
                    : TranMode.Credit;

                var transactionsToProcess = await _context.DoTransactions
                    .Where(t => t.BankTxnDate.Date == targetBankTxnDate.Date
                             && t.TranMode.Equals(targetTranMode, StringComparison.OrdinalIgnoreCase)
                             && t.TranStatus == TranStatus.Success
                             && t.SettlStatus == SettlementStatus.Pending)
                    .ToListAsync();

                if (!transactionsToProcess.Any())
                {
                    return new DataprocessResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = $"No pending {request.SettlementOperation} transactions found for Process on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }

                // ৫. DoTransactions টেবিলে BatchProcessId এবং SettlStatus
                DateTime ProcessDate = DateTime.UtcNow;
                decimal totalAmount = transactionsToProcess.Sum(x => x.TransactionAmount);

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
                    TotalAmount = totalAmount,
                    BankAccountNo = string.Empty,
                    Status = SettlementStatus.Processing,
                    ProcessBrCode = request.BrCode,
                    ProcessedBy = request.UserId,
                    SettlementOperation = request.SettlementOperation,
                    ProcessedAt = ProcessDate,
                };

                await _context.Settlements.AddAsync(settlementRecord);

                // ৮. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return new DataprocessResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = $"Processing completed successfully for {request.SettlementOperation}. BatchProcessId: {batchProcessId}, Total Amount: {totalAmount}"
                };
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during Processing for Operation: {Operation}", request.SettlementOperation);

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
                // ১. রিকোয়েস্টের BankTxnDate পার্স করা
                DateTime targetBankTxnDate = DateTimeHelper.ParseToDateTime(request.BankTxnDate);

                // ২. প্রারম্ভিক চেক: উক্ত তারিখ ও অপারেশনের জন্য ইতোমধ্যে সেটেলমেন্ট/Process করা হয়েছে কিনা
                bool isAlreadySettledOrPending = await _context.Settlements
                    .AnyAsync(s => s.BankTxnDate.Date == targetBankTxnDate.Date
                                && s.SettlementOperation.Equals(request.SettlementOperation, StringComparison.OrdinalIgnoreCase)
                                && (s.Status == SettlementStatus.Settled || s.Status == SettlementStatus.Pending));

                if (isAlreadySettledOrPending)
                {
                    return new SettlementResponse
                    {
                        HttpCode = 409,
                        HttpStatus = "Conflict",
                        Message = $"Settlement has already been Settled or is currently in Process for {request.SettlementOperation} on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }

                // ৪. SettlementOperation অনুযায়ী TranMode নির্ধারণ ও DoTransactions ফিল্টারিং
                string targetTranMode = request.SettlementOperation.Equals(SettlementOperation.Toll, StringComparison.OrdinalIgnoreCase)
                    ? TranMode.Debit
                    : TranMode.Credit;

                var transactionsToSettle = await _context.DoTransactions
                    .Where(t => t.BankTxnDate.Date == targetBankTxnDate.Date
                             && t.TranMode.Equals(targetTranMode, StringComparison.OrdinalIgnoreCase)
                             && t.TranStatus == TranStatus.Success
                             && t.SettlStatus == SettlementStatus.Processing)
                    .ToListAsync();

                if (!transactionsToSettle.Any())
                {
                    return new SettlementResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = $"No Processing {request.SettlementOperation} transactions found for Settle on {targetBankTxnDate:yyyy-MM-dd}."
                    };
                }


                DateTime SettleDate = DateTime.UtcNow;
                decimal totalAmount = transactionsToSettle.Sum(x => x.TransactionAmount);
                var settlementRecord = new Settlement();

                if (request.SettlementOperation == SettlementOperation.TopUp)
                {

                    // Settlements টেবিলে update
                    Settlement? settleRecord = null;

                    if (transactionsToSettle != null && transactionsToSettle.Count > 0)
                    {
                        string batchId = transactionsToSettle[0].BatchProcessId;

                        settleRecord = await _context.Settlements
                            .FirstOrDefaultAsync(t => t.BatchProcessId == batchId);
                    }

                    settleRecord.SettledBy = request.UserId;
                    settleRecord.SettledAt = SettleDate;

                    // DoTransactions টেবিলে SettlStatus update
                    foreach (var txn in transactionsToSettle)
                    {
                        txn.SettlStatus = SettlementStatus.Settled;
                    }

                }
                else
                {
                    await SendToCbsAsync(transactionsToSettle, batchId, totalAmount);
                }

               

                await _context.Settlements.AddAsync(settlementRecord);

                // ৮. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return new SettlementResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = $"Settlement completed successfully for {request.SettlementOperation}, Total Amount: {totalAmount}"
                };
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during Processing for Operation: {Operation}", request.SettlementOperation);

                return new SettlementResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = $"An error occurred during Settlement: {ex.Message}"
                };
            }
        }

        // CBS Integration Placeholder Method
        private async Task SendToCbsAsync(decimal totalAmount, string batchProcessId, string brCode)
        {
            // TODO: CBS API Call Implementation will be added later
           // _logger.LogInformation("Sending Toll Settlement to CBS. BatchProcessId: {BatchId}, Amount: {Amount}, Total Txns: {Count}",
             //   batchProcessId, totalAmount, transactions.Count);

            await Task.CompletedTask;
        }
    }
}