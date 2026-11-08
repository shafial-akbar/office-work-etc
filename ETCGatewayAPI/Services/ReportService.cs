using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using ETCGatewayAPI.Data;
using Microsoft.EntityFrameworkCore;
using Etc.Shared.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
                // ১. ইনডেক্স-বান্ধব Half-open Interval [FromDate, ToDateExclusive)
                var fromDate = request.FromDate.Date;
                var toDateExclusive = request.ToDate.Date.AddDays(1);

                var query = _context.DoTransactions.AsNoTracking();

                // ২. Date Range ফিল্টার
                query = query.Where(t => t.BankTxnDate >= fromDate && t.BankTxnDate < toDateExclusive);

                // ৩. Purpose / ReportType ফিল্টারিং লজিক (C# ভ্যারিয়েবলে চেক হচ্ছে, তাই StringComparison নিরাপদ)
                if (!string.IsNullOrWhiteSpace(request.Purpose) &&
                    request.Purpose.Equals(ReportType.Settlement, StringComparison.OrdinalIgnoreCase))
                {
                    // Settlement Report: শুধুমাত্র সম্পন্ন হওয়া Settled ট্রানজেকশন
                    query = query.Where(t => t.SettlStatus == SettlementStatus.Settled);
                }
                else if (!string.IsNullOrWhiteSpace(request.Purpose) &&
                         request.Purpose.Equals(ReportType.Reconciliation, StringComparison.OrdinalIgnoreCase))
                {
                    // Reconciliation Report: Success এবং Reversed উভয় ট্রানজেকশন
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
                    // Transaction / Default Report: শুধুমাত্র Active Success ট্রানজেকশন
                    query = query.Where(t => t.TranStatus == TranStatus.Success);
                }

                // ৪. TranMode ফিল্টার (Debit/Credit)
                if (!string.IsNullOrWhiteSpace(request.TranMode))
                {
                    var filterTranMode = request.TranMode.Trim().ToLower();

                    // ✅ LINQ SQL Translation Friendly: StringComparison মেথডের বদলে ToLower() ব্যবহার
                    query = query.Where(t => t.TranMode.ToLower() == filterTranMode);

                    // ক্রেডিট মোড ফিল্টার করা থাকলে ইন-হাউস রিফান্ড এন্ট্রি (_REV) বাদ রাখা (যদি না Reversal রিপোর্ট চাওয়া হয়)
                    if (request.TranMode.Equals(TranMode.Credit, StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrWhiteSpace(request.Purpose) || !request.Purpose.Equals(ReportType.Reversal, StringComparison.OrdinalIgnoreCase)))
                    {
                        query = query.Where(t => string.IsNullOrEmpty(t.OriginalBankTxnId) &&
                                                 (t.PartnerTxnId == null || !t.PartnerTxnId.EndsWith("_REV")));
                    }
                }

                // ৫. SourceChannel ফিল্টার
                if (!string.IsNullOrWhiteSpace(request.SourceChannel))
                {
                    var filterSourceChannel = request.SourceChannel.Trim().ToLower();

                    // ✅ LINQ SQL Translation Friendly: StringComparison মেথডের বদলে ToLower() ব্যবহার
                    query = query.Where(t => t.SourceChannel != null && t.SourceChannel.ToLower() == filterSourceChannel);
                }

                // ৬. প্রজেকশন (RefNo1 ডেবিটে গাড়ির নম্বর এবং ক্রেডিটে চ্যানেল রেফারেন্স)
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

                        // Toll Debit Fields
                        PartnerId = t.TranMode == TranMode.Debit ? t.PartnerId : null,
                        PartnerTxnId = t.TranMode == TranMode.Debit ? t.PartnerTxnId : null,

                        // Common / Channel Fields
                        RefNo1 = t.RefNo1,
                        ChannelTransactionDate = t.TranMode == TranMode.Credit ? t.ChannelTransactionDate : null
                    })
                    .ToListAsync();

                // ৭. Summary হিসাব
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

        public async Task<MasterBalanceSummaryResponse> GetMasterBalanceSummaryAsync(DateTime? fromDate, DateTime? toDate, DateTime? asOfDate = null)
        {
            try
            {
                // ১. তারিখ রেঞ্জ নির্ধারণ
                DateTime effectiveToDate = (toDate ?? asOfDate ?? DateTime.UtcNow).Date;
                DateTime toDateExclusive = effectiveToDate.AddDays(1);
                DateTime? effectiveFromDate = fromDate?.Date;

                decimal openingBalance = 0m;

                // ২. Opening Balance গণনা (FromDate-এর পূর্ববর্তী সময়ের ব্যালেন্স)
                if (effectiveFromDate.HasValue)
                {
                    DateTime fromBoundary = effectiveFromDate.Value;

                    var openingSummary = await _context.DoTransactions
                        .Where(t => t.BankTxnDate < fromBoundary)
                        .GroupBy(t => 1)
                        .Select(g => new
                        {
                            // প্রকৃত টপ-আপ (রিফান্ড ক্রেডিট বাদ দিয়ে)
                            TopUp = g.Sum(x => x.TranMode.Equals(TranMode.Credit, StringComparison.OrdinalIgnoreCase)
                                            && x.TranStatus == TranStatus.Success
                                            && string.IsNullOrEmpty(x.OriginalBankTxnId)
                                            && (x.PartnerTxnId == null || !x.PartnerTxnId.EndsWith("_REV")) ? x.TransactionAmount : 0m),

                            // মোট টোল কালেকশন চেষ্টা (Success + Reversed)
                            GrossToll = g.Sum(x => x.TranMode.Equals(TranMode.Debit, StringComparison.OrdinalIgnoreCase)
                                                && (x.TranStatus == TranStatus.Success || x.TranStatus == TranStatus.Reversed) ? x.TransactionAmount : 0m),

                            // বাতিল হওয়া টোল
                            ReversedToll = g.Sum(x => x.TranMode.Equals(TranMode.Debit, StringComparison.OrdinalIgnoreCase)
                                                   && x.TranStatus == TranStatus.Reversed ? x.TransactionAmount : 0m)
                        })
                        .FirstOrDefaultAsync();

                    if (openingSummary != null)
                    {
                        decimal openingNetToll = openingSummary.GrossToll - openingSummary.ReversedToll;
                        openingBalance = openingSummary.TopUp - openingNetToll;
                    }
                }

                // ৩. নির্ধারিত সময়ের (Period) ডেটা গণনা
                var periodQuery = _context.DoTransactions.AsQueryable();

                if (effectiveFromDate.HasValue)
                {
                    periodQuery = periodQuery.Where(t => t.BankTxnDate >= effectiveFromDate.Value && t.BankTxnDate < toDateExclusive);
                }
                else
                {
                    periodQuery = periodQuery.Where(t => t.BankTxnDate < toDateExclusive);
                }

                var periodSummary = await periodQuery
                    .GroupBy(t => 1)
                    .Select(g => new
                    {
                        // প্রকৃত টপ-আপ (রিফান্ড ক্রেডিট বাদ)
                        TotalTopUp = g.Sum(x => x.TranMode.Equals(TranMode.Credit, StringComparison.OrdinalIgnoreCase)
                                             && x.TranStatus == TranStatus.Success
                                             && string.IsNullOrEmpty(x.OriginalBankTxnId)
                                             && (x.PartnerTxnId == null || !x.PartnerTxnId.EndsWith("_REV")) ? x.TransactionAmount : 0m),

                        // গ্রস টোল (Success + Reversed)
                        GrossToll = g.Sum(x => x.TranMode.Equals(TranMode.Debit, StringComparison.OrdinalIgnoreCase)
                                             && (x.TranStatus == TranStatus.Success || x.TranStatus == TranStatus.Reversed) ? x.TransactionAmount : 0m),

                        // রিভার্সড টোল
                        ReversedToll = g.Sum(x => x.TranMode.Equals(TranMode.Debit, StringComparison.OrdinalIgnoreCase)
                                                && x.TranStatus == TranStatus.Reversed ? x.TransactionAmount : 0m)
                    })
                    .FirstOrDefaultAsync();

                decimal periodTopUp = periodSummary?.TotalTopUp ?? 0m;
                decimal periodGrossToll = periodSummary?.GrossToll ?? 0m;
                decimal periodReversedToll = periodSummary?.ReversedToll ?? 0m;

                // ৪. নিখুঁত গাণিতিক সমীকরণ
                decimal periodNetToll = periodGrossToll - periodReversedToll;
                decimal closingBalance = openingBalance + periodTopUp - periodNetToll;

                return new MasterBalanceSummaryResponse
                {
                    OpeningBalance = openingBalance,
                    TotalTopUpAmount = periodTopUp,
                    GrossTollAmount = periodGrossToll,
                    ReversedTollAmount = periodReversedToll,
                    NetTollAmount = periodNetToll,
                    ClosingBalance = closingBalance,
                    FromDate = effectiveFromDate,
                    ToDate = effectiveToDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating Master Balance Summary Report.");
                throw;
            }
        }
    }
}