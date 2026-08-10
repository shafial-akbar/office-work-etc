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
    public class ReportService : IReportService
    {
        private readonly DatabaseContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ReportService> _logger;

        public ReportService(DatabaseContext context,
                               IHttpContextAccessor httpContextAccessor,
                               ILogger<ReportService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<ReportResponse> GetReportAsync(ReportRequest request)
        {
            try
            {
                // ১. তারিখের Boundary সেট করা (ToDate এর একদম শেষ মুহূর্ত ২৩:৫৯:৫৯ পর্যন্ত কভার করার জন্য)
                var fromDate = request.FromDate.Date;
                var toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);

                var query = _context.DoTransactions.AsNoTracking();

                // ২. Purpose / ReportType ফিল্টারিং লজিক
                if (!string.IsNullOrWhiteSpace(request.Purpose) &&
                    request.Purpose.Equals(ReportType.Settlement, StringComparison.OrdinalIgnoreCase))
                {
                    // Settlement Report: শুধুমাত্র EOD Batch সম্পূর্ণ হওয়া Settled ট্রানজেকশন
                    query = query.Where(t => t.SettlStatus == SettlementStatus.Settled);
                }
                else if (!string.IsNullOrWhiteSpace(request.Purpose) &&
                         request.Purpose.Equals(ReportType.Reconciliation, StringComparison.OrdinalIgnoreCase))
                {
                    // Reconciliation Report: Success এবং Reversed উভয় ট্রানজেকশন (Toll Authority & Top-up Channels)
                    query = query.Where(t => t.TranStatus == TranStatus.Success || t.TranStatus == TranStatus.Reversed);
                }
                else if (!string.IsNullOrWhiteSpace(request.Purpose) &&
                         request.Purpose.Equals(ReportType.Reversal, StringComparison.OrdinalIgnoreCase))
                {
                    // Reversal Report: শুধুমাত্র রিভার্সড ট্রানজেকশন
                    query = query.Where(t => t.TranStatus == TranStatus.Reversed);
                }
                else
                {
                    // Transaction / Default Report: শুধুমাত্র Active Success ট্রানজেকশন (Reversed বাদে)
                    query = query.Where(t => t.TranStatus == TranStatus.Success);
                }

                // ৩. Date Range ফিল্টার
                query = query.Where(t => t.BankTxnDate >= fromDate && t.BankTxnDate <= toDate);

                // ৪. TranMode ফিল্টার (Debit/Credit)
                if (!string.IsNullOrWhiteSpace(request.TranMode))
                {
                    query = query.Where(t => t.TranMode.Equals(request.TranMode, StringComparison.OrdinalIgnoreCase));
                }

                // ৫. SourceChannel ফিল্টার: শুধুমাত্র Credit Mode এর জন্য এবং যদি রিকোয়েস্টে পাঠানো হয়
                bool isCreditMode = !string.IsNullOrWhiteSpace(request.TranMode) &&
                                    request.TranMode.Equals(TranMode.Credit, StringComparison.OrdinalIgnoreCase);

                if (isCreditMode && !string.IsNullOrWhiteSpace(request.SourceChannel))
                {
                    query = query.Where(t => t.SourceChannel.Equals(request.SourceChannel, StringComparison.OrdinalIgnoreCase));
                }

                // ৬. LINQ to Entities প্রজেকশন
                var details = await query
                    .OrderByDescending(t => t.BankTxnDate)
                    .Select(t => new ReportDetailDto
                    {
                        Id = t.Id,
                        BankTxnId = t.BankTxnId,
                        BankTxnDate = t.BankTxnDate,
                        SourceAccountNo = t.SourceAccountNo,
                        TransactionAmount = t.TransactionAmount,
                        TranMode = t.TranMode,
                        SourceChannel = t.SourceChannel,
                        TranStatus = t.TranStatus,
                        SettlStatus = t.SettlStatus,

                        // Debit Mode ফিল্ডস (Partner Information)
                        PartnerId = t.TranMode == TranMode.Debit ? t.PartnerId : null,
                        PartnerTxnId = t.TranMode == TranMode.Debit ? t.PartnerTxnId : null,

                        // Credit Mode ফিল্ডস (Channel Ref Information)
                        RefNo1 = t.TranMode == TranMode.Credit ? t.RefNo1 : null,
                        ChannelTransactionDate = t.TranMode == TranMode.Credit ? t.ChannelTransactionDate : null
                    })
                    .ToListAsync();

                // ৭. Summary হিসাব করা
                var summary = new ReportSummaryDto
                {
                    TotalTransactions = details.Count,
                    TotalAmount = details.Sum(d => d.TransactionAmount),
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    FilteredTranMode = !string.IsNullOrWhiteSpace(request.TranMode) ? request.TranMode.ToUpper() : "ALL"
                };

                return new ReportResponse
                {
                    Summary = summary,
                    Details = details
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report for Date Range: {FromDate} to {ToDate}, Purpose: {Purpose}",
                    request.FromDate, request.ToDate, request.Purpose);
                throw;
            }
        }
    }
}