using System;
using System.Threading.Tasks;
using Etc.Shared.Constants;
using Etc.Shared.DTOs;
using Etc.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ETCAdminUI.Controllers
{
    //[Authorize]
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;
        private readonly IWalletTransactionService _walletTransactionService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(IReportService reportService, IWalletTransactionService walletTransactionService,  ILogger<ReportController> logger)
        {
            _reportService = reportService;
            _walletTransactionService = walletTransactionService;
            _logger = logger;
        }

        // GET: /Report অথবা /Report/Index
        // ট্রানজেকশন, রিকনসিলিয়েশন, সেটেলমেন্ট এবং রিভার্সাল রিপোর্ট ভিউ
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] ReportRequest request)
        {
            try
            {
                // প্রথমবার পেজ লোড হলে ডিফল্ট ডেট রেঞ্জ হিসেবে আজকের তারিখ সেট করা
                if (request.FromDate == default)
                {
                    request.FromDate = DateTime.Today;
                }

                if (request.ToDate == default)
                {
                    request.ToDate = DateTime.Today;
                }

                if (request.FromDate > request.ToDate)
                {
                    ModelState.AddModelError("DateRange", "From Date cannot be greater than To Date.");
                    ViewBag.Filter = request;
                    return View(new ReportResponse());
                }

                // ভিউতে ফিল্টার ফর্মের সিলেক্ট করা মানগুলো ধরে রাখার জন্য ViewBag ব্যবহার
                ViewBag.Filter = request;

                var reportResult = await _reportService.GetReportAsync(request);
                return View(reportResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Report Index in ETCAdminUI");
                TempData["ErrorMessage"] = "Failed to generate report. Please try again.";
                ViewBag.Filter = request;
                return View(new ReportResponse());
            }
        }

        // GET: /Report/MasterBalance
        // মাস্টার ভার্চুয়াল ওয়ালেট ও সিবিএস জিএল ব্যালেন্স সামারি ভিউ
        [HttpGet]
        public async Task<IActionResult> MasterBalance([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] DateTime? asOfDate)
        {
            try
            {
                // ডিফল্ট ফিল্টার ভ্যালু
                DateTime effectiveTo = toDate ?? asOfDate ?? DateTime.Today;
                DateTime? effectiveFrom = fromDate ?? DateTime.Today.AddDays(-7); // ডিফল্ট বিগত ৭ দিন

                ViewBag.FromDate = effectiveFrom?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = effectiveTo.ToString("yyyy-MM-dd");

                var summaryResult = await _reportService.GetMasterBalanceSummaryAsync(effectiveFrom, effectiveTo, asOfDate);
                return View(summaryResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Master Balance Summary in ETCAdminUI");
                TempData["ErrorMessage"] = "Failed to load master balance summary.";
                return View(new MasterBalanceSummaryResponse());
            }
        }

        // GET: /Report/ExportCsv (ঐচ্ছিক: অ্যাডমিনদের ডেটা এক্সপোর্ট দেওয়ার জন্য)
        [HttpGet]
        public async Task<IActionResult> ExportCsv([FromQuery] ReportRequest request)
        {
            if (request.FromDate == default) request.FromDate = DateTime.Today;
            if (request.ToDate == default) request.ToDate = DateTime.Today;

            var reportResult = await _reportService.GetReportAsync(request);

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("BankTxnId,BankTxnDate,TranMode,Amount,SourceChannel,TranStatus,SettlStatus,RefNo1");

            foreach (var item in reportResult.Details)
            {
                builder.AppendLine($"\"{item.BankTxnId}\",\"{item.BankTxnDate:yyyy-MM-dd HH:mm:ss}\",\"{item.TranMode}\",{item.TransactionAmount},\"{item.SourceChannel}\",\"{item.TranStatus}\",\"{item.SettlStatus}\",\"{item.RefNo1}\"");
            }

            return File(System.Text.Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"ETC_Report_{DateTime.Now:yyyyMMddHHmmss}.csv");
        }

        // GET: /Report/Reconcile
        [HttpGet]
        public async Task<IActionResult> Reconcile(
            [FromQuery] string? searchType,
            [FromQuery] string? searchValue,
            [FromQuery] string? partnerTxnId,
            [FromQuery] string? referenceId)
        {
            // সরাসরি কোয়েরি স্ট্রিং থেকে আইডি আসলে তা অগ্রাধিকার পাবে
            if (!string.IsNullOrWhiteSpace(partnerTxnId))
            {
                searchType = "Toll";
                searchValue = partnerTxnId.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(referenceId))
            {
                searchType = "SBL";
                searchValue = referenceId.Trim();
            }
            else
            {
                searchType = string.IsNullOrWhiteSpace(searchType) ? "Toll" : searchType.Trim();
                searchValue = searchValue?.Trim();
            }

            ViewBag.SearchType = searchType;
            ViewBag.SearchValue = searchValue;

            // প্রথমবার পেজে প্রবেশ করলে কোনো সার্চ চালাবে না
            if (string.IsNullOrWhiteSpace(searchValue))
            {
                return View(null);
            }

            // অথরিটি টাইপ অনুযায়ী রিকোয়েস্ট অবজেক্ট তৈরি
            var request = new ReconcileTransactionRequest
            {
                PartnerTxnId = searchType.Equals("Toll", StringComparison.OrdinalIgnoreCase) ? searchValue : null,
                ReferenceId = searchType.Equals("SBL", StringComparison.OrdinalIgnoreCase) ? searchValue : null
            };

            try
            {
                var response = await _walletTransactionService.ReconcileTransactionAsync(request);
                return View(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reconciling transaction for {SearchType}: {SearchValue}", searchType, searchValue);
                TempData["ErrorMessage"] = "Reconciliation search encountered an unexpected system error.";
                return View(null);
            }
        }
    }
}