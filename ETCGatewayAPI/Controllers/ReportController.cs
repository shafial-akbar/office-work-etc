using System;
using System.Threading.Tasks;
using Etc.Shared.DTOs;
using Etc.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ETCGatewayAPI.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(IReportService reportService, ILogger<ReportController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        [HttpGet("report")]
        public async Task<IActionResult> GetReport([FromQuery] ReportRequest request)
        {
            try
            {
                var result = await _reportService.GetReportAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpGet("masterBalance")]
        public async Task<IActionResult> GetMasterBalanceSummary(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] DateTime? asOfDate = null)
        {
            try
            {
                var result = await _reportService.GetMasterBalanceSummaryAsync(fromDate, toDate, asOfDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching master balance summary");
                return StatusCode(500, new { Message = ex.Message });
            }
        }
    }
}