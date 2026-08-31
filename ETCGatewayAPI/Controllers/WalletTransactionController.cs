using Etc.Shared.DTOs;
using Etc.Shared.Interfaces;
using ETCGatewayAPI.Services;
using EtcMwApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCGatewayAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WalletTransactionController : ControllerBase
    {
        private readonly ICustomerInquiryService _inquiryService;
        private readonly WalletTransactionService _walletTransactionService;

        public WalletTransactionController(
            ICustomerInquiryService inquiryService,
            WalletTransactionService walletTransactionService)
        {
            _inquiryService = inquiryService;
            _walletTransactionService = walletTransactionService;
        }

        [HttpGet("check-account")]
        public async Task<IActionResult> CheckAccount([FromQuery] string mobileNo)
        {
            if (string.IsNullOrWhiteSpace(mobileNo))
            {
                var badRequestResponse = new { Message = "Mobile number is required." };
                return BadRequest(badRequestResponse);
            }

            try
            {
                var result = await _inquiryService.CheckAccountByMobileAsync(mobileNo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                return StatusCode(500, errorResponse);
            }
        }

        [HttpGet("check-balance")]
        public async Task<IActionResult> CheckBalance([FromQuery] string searchKey)
        {

            if (string.IsNullOrWhiteSpace(searchKey))
            {
                var badRequestResponse = new { Message = "Mobile number or Wallet No is required." };
                return BadRequest(badRequestResponse);
            }

            try
            {
                var result = await _inquiryService.GetWalletBalanceAsync(searchKey);
                return Ok(result);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                return StatusCode(500, errorResponse);
            }
        }

        // ১. ওয়ালেট টপ-আপ (Wallet Credit)
        [HttpPost("top-up")]
        public async Task<IActionResult> TopUpWallet([FromBody] TopUpRequest topUpDto)
        {

            if (topUpDto == null)
            {
                var badRequestResponse = new { Message = "Invalid top-up request payload." };
                return BadRequest(badRequestResponse);
            }

            try
            {
                var result = await _walletTransactionService.TopUpWalletAsync(topUpDto);

                return StatusCode(result.HttpCode, result);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                return StatusCode(500, errorResponse);
            }
        }

        // ২. টোল কালেকশন ও ব্যালেন্স কাটা (Toll Amount Debit/Deduction)
        [HttpPost("deduct-toll")]
        public async Task<IActionResult> DeductToll([FromBody] DoTransactionRequest deductionDto)
        {

            if (deductionDto == null)
            {
                var badRequestResponse = new { Message = "Invalid toll deduction request payload." };
                return BadRequest(badRequestResponse);
            }

            try
            {
                var result = await _walletTransactionService.DeductTollAsync(deductionDto);

                return StatusCode(result.HttpCode, result);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                return StatusCode(500, errorResponse);
            }
        }

        // ৩. টোল ট্রানজেকশন রিভার্সাল বা রিফান্ড (Toll Amount Reversal/Credit)
        [HttpPost("reverse-toll")]
        public async Task<IActionResult> ReverseToll([FromBody] ReverseTransactionRequest reversalDto)
        {

            if (reversalDto == null)
            {
                var badRequestResponse = new { Message = "Invalid reversal request payload." };
                return BadRequest(badRequestResponse);
            }

            try
            {
                var result = await _walletTransactionService.ReverseTollAsync(reversalDto);

                return StatusCode(result.HttpCode, result);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                return StatusCode(500, errorResponse);
            }
        }

        // ৪. Toll Authority-র জন্য Reconcile Endpoint (PartnerTxnId)
        [HttpPost("reconcile-toll")]
        public async Task<IActionResult> ReconcileToll([FromBody] ReconcileTransactionRequest request)
        {

            if (string.IsNullOrWhiteSpace(request?.PartnerTxnId))
            {
                var badRequestResponse = new { Message = "PartnerTxnId is required for toll reconciliation." };
                return BadRequest(badRequestResponse);
            }

            try
            {
                request.ReferenceId = null;

                var result = await _walletTransactionService.ReconcileTransactionAsync(request);

                return StatusCode(result.HttpCode, result);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                return StatusCode(500, errorResponse);
            }
        }

        // ৫. SBL Channels-এর জন্য Reconcile Endpoint (ReferenceId)
        [HttpPost("reconcile-channel")]
        public async Task<IActionResult> ReconcileChannel([FromBody] ReconcileTransactionRequest request)
        {

            if (string.IsNullOrWhiteSpace(request?.ReferenceId))
            {
                var badRequestResponse = new { Message = "ReferenceId is required for channel reconciliation." };
                return BadRequest(badRequestResponse);
            }

            try
            {
                request.PartnerTxnId = null;

                var result = await _walletTransactionService.ReconcileTransactionAsync(request);

                return StatusCode(result.HttpCode, result);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                return StatusCode(500, errorResponse);
            }
        }
    }
}