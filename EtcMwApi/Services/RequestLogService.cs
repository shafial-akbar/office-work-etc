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

        public async Task<Guid> LogRequest(HttpRequest request)
        {
            try
            {
                request.EnableBuffering();

                // StreamReader শেষ হওয়ার পর স্ট্রিম যেন ক্লোজ না হয়ে যায়, তাই leaveOpen: true রাখা নিরাপদ
                using var reader = new StreamReader(request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                request.Body.Position = 0;

                var logEntry = new RequestLog
                {
                    Id = Guid.NewGuid(), // নতুন Guid তৈরি করা
                    RequestMethod = request.Method,
                    RequestPath = request.Path,
                    RequestQuery = request.QueryString.Value,
                    RequestHeaders = JsonSerializer.Serialize(request.Headers),
                    RequestPayload = body,
                    RequestTime = DateTime.UtcNow,
                    ClientIp = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                    UserAgent = request.Headers["User-Agent"].ToString()
                };

                await _context.RequestLogs.AddAsync(logEntry);
                await _context.SaveChangesAsync();

                return logEntry.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging request");
                return Guid.Empty; // int -1 এর জায়গায় Guid.Empty রিটার্ন করুন
            }
        }

        public async Task LogResponse(Guid logId, object response)
        {
            // 1. Guid.Empty হলে কোনো কাজ করবে না
            if (logId == Guid.Empty) return;

            try
            {
                var logEntry = await _context.RequestLogs.FindAsync(logId);
                if (logEntry == null) return;

                logEntry.ResponsePayload = JsonSerializer.Serialize(response);
                logEntry.ResponseTime = DateTime.UtcNow;

                if (logEntry.RequestTime != default)
                {
                    logEntry.DurationMs = (long)(logEntry.ResponseTime.Value - logEntry.RequestTime).TotalMilliseconds;
                }

                // 2. Dynamic / Reflection দিয়ে StatusCode রিড করা (ApiResponse<T> এবং Anonymous Object দুটোর জন্যই কাজ করবে)
                if (response is IActionResult actionResult && actionResult is ObjectResult objectResult)
                {
                    logEntry.StatusCode = objectResult.StatusCode ?? 200;
                }
                else if (response != null)
                {
                    var statusCodeProperty = response.GetType().GetProperty("StatusCode")?.GetValue(response);
                    if (statusCodeProperty is int code && code > 0)
                    {
                        logEntry.StatusCode = code;
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging response for LogId: {LogId}", logId);
            }
        }

        public async Task LogResponse<T>(Guid logId, ApiResponse<T> response)
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
