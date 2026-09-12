using Microsoft.AspNetCore.Mvc;
using ETCAdminUI.Services; // আপনার সঠিক সার্ভিস নেমস্পেস দিন
using Etc.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Etc.Shared.Interfaces;
using ETCAdminUI.Models;

namespace ETCAdminUI.Controllers
{
    public class SettlementController : Controller
    {
        private readonly ISettlementService _settlementService;

        public SettlementController(ISettlementService settlementService)
        {
            _settlementService = settlementService;
        }

        // GET: Settlement/Index (মেইন ড্যাশবোর্ড বা ইন্টারফেস)
        public IActionResult Index()
        {
            // সেশন থেকে ইউজার ইনফরমেশন চেক করা (নিরাপত্তার জন্য)
            var userInfoStr = HttpContext.Session.GetString("loginStatus");
            if (string.IsNullOrEmpty(userInfoStr))
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        // POST: Settlement/DoDataprocess
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoDataprocess(string bankTxnDate, string settlementOperation)
        {
            var userInfoStr = HttpContext.Session.GetString("loginStatus");
            if (string.IsNullOrEmpty(userInfoStr)) return Json(new { httpCode = 401, message = "Unauthorized access." });

            var user = JsonConvert.DeserializeObject<UserInfoModel>(userInfoStr);

            var request = new DataprocessRequest
            {
                BankTxnDate = bankTxnDate,
                SettlementOperation = settlementOperation,
                BrCode = user.UserBRCode, // সেশন থেকে ব্রাঞ্চ কোড
                UserId = user.UserId      // সেশন থেকে ইউজার আইডি
            };

            if (!ModelState.IsValid)
            {
                return Json(new { httpCode = 400, message = "Invalid input data." });
            }

            var response = await _settlementService.DoDataprocessAsync(request);
            return Json(new { httpCode = response.HttpCode, httpStatus = response.HttpStatus, message = response.Message });
        }

        // POST: Settlement/DoSettlement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoSettlement(string bankTxnDate, string settlementOperation)
        {
            var userInfoStr = HttpContext.Session.GetString("loginStatus");
            if (string.IsNullOrEmpty(userInfoStr)) return Json(new { httpCode = 401, message = "Unauthorized access." });

            var user = JsonConvert.DeserializeObject<UserInfoModel>(userInfoStr);

            var request = new SettlementRequest
            {
                BankTxnDate = bankTxnDate,
                SettlementOperation = settlementOperation,
                BrCode = user.UserBRCode,
                UserId = user.UserId
            };

            if (!ModelState.IsValid)
            {
                return Json(new { httpCode = 400, message = "Invalid input data." });
            }

            var response = await _settlementService.DoSettlementAsync(request);
            return Json(new { httpCode = response.HttpCode, httpStatus = response.HttpStatus, message = response.Message });
        }
    }
}
