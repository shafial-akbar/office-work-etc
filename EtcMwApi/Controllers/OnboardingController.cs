using Etc.Shared.DTOs;
using Etc.Shared.Interfaces;
using EtcMwApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EtcMwApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // 🔒 পুরো কন্ট্রোলারের সব API সুরক্ষিত থাকবে
    public class OnboardingController : ControllerBase
    {
        private readonly ICustomerOnboardingService _onboardingService;
        private readonly ICustomerInquiryService _inquiryService;
        private readonly IRequestLogService _requestLogService;

        public OnboardingController(
            ICustomerOnboardingService onboardingService,
            ICustomerInquiryService inquiryService,
            IRequestLogService requestLogService)
        {
            _onboardingService = onboardingService;
            _inquiryService = inquiryService;
            _requestLogService = requestLogService;
        }

        [HttpGet("check-account")]
        public async Task<IActionResult> CheckAccount([FromQuery] string mobileNo)
        {
            var logId = await _requestLogService.LogRequest(Request);

            if (string.IsNullOrWhiteSpace(mobileNo))
            {
                var badRequestResponse = new { Message = "Mobile number is required." };
                await _requestLogService.LogResponse(logId, badRequestResponse);
                return BadRequest(badRequestResponse);
            }

            try
            {
                var result = await _inquiryService.CheckAccountByMobileAsync(mobileNo);
                await _requestLogService.LogResponse(logId, result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                await _requestLogService.LogResponse(logId, errorResponse);
                return StatusCode(500, errorResponse);
            }
        }

        [HttpGet("check-balance")]
        public async Task<IActionResult> CheckBalance([FromQuery] string searchKey)
        {
            var logId = await _requestLogService.LogRequest(Request);

            if (string.IsNullOrWhiteSpace(searchKey))
            {
                var badRequestResponse = new
                {
                    Success = false,
                    Message = "Mobile number or Wallet No is required.",
                    Data = (object)null
                };
                await _requestLogService.LogResponse(logId, badRequestResponse);
                return BadRequest(badRequestResponse);
            }

            try
            {
                var result = await _inquiryService.GetWalletBalanceAsync(searchKey);

                // ১. ডাটা না পাওয়া গেলে (Count == 0)
                if (result == null || !result.Any())
                {
                    var notFoundResponse = new
                    {
                        Success = false,
                        Message = "No active wallet found with the provided search key.",
                        Data = new List<WalletBalanceResultDto>()
                    };

                    await _requestLogService.LogResponse(logId, notFoundResponse);

                    // রিকোয়ারমেন্ট অনুযায়ী 404 NotFound অথবা 200 OK যেকোনোটি দিতে পারেন
                    return NotFound(notFoundResponse);
                }

                // ২. ডাটা পাওয়া গেলে
                var successResponse = new
                {
                    Success = true,
                    Message = "Wallet balance fetched successfully.",
                    Data = result
                };

                await _requestLogService.LogResponse(logId, successResponse);
                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    Success = false,
                    Message = ex.Message,
                    Data = (object)null
                };
                await _requestLogService.LogResponse(logId, errorResponse);
                return StatusCode(500, errorResponse);
            }
        }

        [HttpPost("enroll-customer")]
        public async Task<IActionResult> EnrollCustomer([FromBody] RegisterFullCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var logId = await _requestLogService.LogRequest(Request);

            try
            {
                var customer = await _onboardingService.RegisterFullCustomerAsync(dto);
                var response = new { Success = true, Message = "New customer enrolled with virtual wallet and Vehicle.", Data = customer };

                await _requestLogService.LogResponse(logId, response);
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                await _requestLogService.LogResponse(logId, errorResponse);
                return NotFound(errorResponse);
            }
            catch (InvalidOperationException ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                await _requestLogService.LogResponse(logId, errorResponse);
                return Conflict(errorResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = "An unexpected error occurred." };
                await _requestLogService.LogResponse(logId, new { Success = false, Exception = ex.Message });
                return StatusCode(500, errorResponse);
            }

        }

        [HttpPost("add-vehicle")]
        public async Task<IActionResult> AddVehicle([FromBody] AddVehicleToWalletDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var logId = await _requestLogService.LogRequest(Request);

            try
            {
                var vehicle = await _onboardingService.AddVehicleToWalletAsync(dto);
                var response = new { Success = true, Message = "Vehicle linked to wallet successfully.", Data = vehicle };

                await _requestLogService.LogResponse(logId, response);
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                await _requestLogService.LogResponse(logId, errorResponse);
                return NotFound(errorResponse);
            }
            catch (InvalidOperationException ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                await _requestLogService.LogResponse(logId, errorResponse);
                return Conflict(errorResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = "An unexpected error occurred." };
                await _requestLogService.LogResponse(logId, new { Success = false, Exception = ex.Message });
                return StatusCode(500, errorResponse);
            }
        }

        [HttpPost("create-wallet")]
        public async Task<IActionResult> CreateWallet([FromBody] CreateNewWalletDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var logId = await _requestLogService.LogRequest(Request);

            try
            {
                var wallet = await _onboardingService.CreateNewWalletAsync(dto);
                var response = new { Success = true, Message = "New wallet created successfully.", Data = wallet };

                await _requestLogService.LogResponse(logId, response);
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                await _requestLogService.LogResponse(logId, errorResponse);
                return NotFound(errorResponse);
            }
            catch (InvalidOperationException ex)
            {
                var errorResponse = new { Success = false, Message = ex.Message };
                await _requestLogService.LogResponse(logId, errorResponse);
                return Conflict(errorResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = new { Success = false, Message = "An unexpected error occurred." };
                await _requestLogService.LogResponse(logId, new { Success = false, Exception = ex.Message });
                return StatusCode(500, errorResponse);
            }
        }

        [HttpPost("unregister-vehicle")]
        public async Task<IActionResult> UnregisterVehicle([FromBody] VehicleUnregisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // ১. Incoming Request Log করে Log ID নিন
            var logId = await _requestLogService.LogRequest(Request);

            try
            {
                // ২. Business Logic/Service Call সম্পন্ন করুন
                var result = await _onboardingService.UnregisterVehicleAsync(request);

                // ৩. Response টি Log ID দিয়ে লিঙ্ক করে Save করুন
                await _requestLogService.LogResponse(logId, result);

                if (result.Success)
                {
                    return Ok(result);
                }

                return StatusCode(result.StatusCode > 0 ? result.StatusCode : 500, result);
            }
            catch (Exception ex)
            {
                // ৪. Unexpected Exception ঘটলেও Response Log নিশ্চিত করুন
                var errorResponse = new
                {
                    Success = false,
                    Reason = "EXCEPTION",
                    Message = "An unexpected error occurred while processing the unregistration request.",
                    StatusCode = 500
                };

                await _requestLogService.LogResponse(logId, new { Success = false, Exception = ex.Message });
                return StatusCode(500, errorResponse);
            }
        }
    }
}