using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using EtcMwApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.Json;

namespace EtcMwApi.Services
{
    public class RequestLogService : IRequestLogService
    {
        private readonly DatabaseContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<RequestLogService> _logger;

        public RequestLogService(DatabaseContext context,
                               IHttpContextAccessor httpContextAccessor,
                               ILogger<RequestLogService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<int> LogRequest(HttpRequest request)
        {
            try
            {
                request.EnableBuffering();
                var body = await new StreamReader(request.Body).ReadToEndAsync();
                request.Body.Position = 0;

                var logEntry = new RequestLog
                {
                    RequestMethod = request.Method,
                    RequestPath = request.Path,
                    RequestQuery = request.QueryString.Value,
                    RequestHeaders = JsonSerializer.Serialize(request.Headers),
                    RequestPayload = body,
                    RequestTime = DateTime.UtcNow,
                    ClientIp = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                    UserAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"]
                };

                await _context.RequestLogs.AddAsync(logEntry);
                await _context.SaveChangesAsync();

                return logEntry.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging request");
                return -1;
            }
        }

        public async Task LogResponse(int logId, object response)
        {
            if (logId <= 0) return;

            try
            {
                var logEntry = await _context.RequestLogs.FindAsync(logId);
                if (logEntry == null) return;

                logEntry.ResponsePayload = JsonSerializer.Serialize(response);
                logEntry.ResponseTime = DateTime.UtcNow;
                logEntry.DurationMs = (long)(logEntry.ResponseTime.Value - logEntry.RequestTime).TotalMilliseconds;

                if (response is ApiResponse apiResponse)
                {
                    logEntry.StatusCode = apiResponse.StatusCode;
                }
                else if (response is IActionResult actionResult && actionResult is ObjectResult objectResult)
                {
                    logEntry.StatusCode = objectResult.StatusCode ?? 200;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging response");
            }
        }


        public async Task LogResponse<T>(int logId, ApiResponse<T> response)
        {
            await LogResponse(logId, (object)response);
        }

        public async Task<IEnumerable<RequestLog>> GetLogs(DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.RequestLogs.AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(x => x.RequestTime >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(x => x.RequestTime <= toDate.Value);
            }

            return await query.OrderByDescending(x => x.RequestTime).ToListAsync();
        }

        
    }
}
