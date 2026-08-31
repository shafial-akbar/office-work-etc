using Etc.Shared.DTOs;
using Etc.Shared.Interfaces;
using Etc.Shared.Constants;
using ETCGatewayAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace EtcMwApi.Controllers
{
    [ApiController]
    //[Route("api/[controller]")]
    [Route("api")]
    [Authorize]
    public class SettlementController : ControllerBase
    {
        private readonly ISettlementService _settlementService;
        private readonly IReportService _reportService;
        private readonly IRequestLogService _requestLogService;

        //public SettlementController(
        //    ISettlementService settlementService,
        //    IRequestLogService requestLogService)
        //{
        //    _settlementService = settlementService;
        //    _requestLogService = requestLogService;
        //}

        //// ১. Toll Authority-র জন্য Settlement Endpoint (Force Debit Mode)
        //[HttpPost("settlement-toll")]
        //public async Task<IActionResult> DoDataprocessAsync([FromBody] DataprocessRequest request)
        //{
        //    var logId = await _requestLogService.LogRequest(Request);

        //    if (request == null || request.FromDate == default || request.ToDate == default)
        //    {
        //        var badRequestResponse = new { Message = "Valid FromDate and ToDate are required." };
        //        await _requestLogService.LogResponse(logId, badRequestResponse);
        //        return BadRequest(badRequestResponse);
        //    }

        //    try
        //    {
        //        // টোল অথরিটির জন্য TranMode ফোর্স করে Debit সেট করা হলো
        //        request.TranMode = TranMode.Debit;

        //        var result = await _settlementService.GetSettlementReportAsync(request);
        //        await _requestLogService.LogResponse(logId, result);

        //        return Ok(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        var errorResponse = new { Success = false, Message = ex.Message };
        //        await _requestLogService.LogResponse(logId, errorResponse);
        //        return StatusCode(500, errorResponse);
        //    }
        //}

        //// ২. SBL Channels-এর জন্য Settlement Endpoint (Force Credit Mode)
        //[HttpPost("settlement-channel")]
        //public async Task<IActionResult> GetChannelSettlementReport([FromBody] ReportRequest request)
        //{
        //    var logId = await _requestLogService.LogRequest(Request);

        //    if (request == null || request.FromDate == default || request.ToDate == default)
        //    {
        //        var badRequestResponse = new { Message = "Valid FromDate and ToDate are required." };
        //        await _requestLogService.LogResponse(logId, badRequestResponse);
        //        return BadRequest(badRequestResponse);
        //    }

        //    try
        //    {
        //        // এসবিএল চ্যানেলের জন্য TranMode ফোর্স করে Credit সেট করা হলো
        //        request.TranMode = TranMode.Credit;

        //        var result = await _settlementService.GetSettlementReportAsync(request);
        //        await _requestLogService.LogResponse(logId, result);

        //        return Ok(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        var errorResponse = new { Success = false, Message = ex.Message };
        //        await _requestLogService.LogResponse(logId, errorResponse);
        //        return StatusCode(500, errorResponse);
        //    }
        //}
    }
}