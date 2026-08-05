using Microsoft.AspNetCore.Http;
using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface IRequestLogService
    {
        Task<int> LogRequest(HttpRequest request);
        Task LogResponse<T>(int logId, ApiResponse<T> response);
        Task LogResponse(int logId, object response);
        Task<IEnumerable<RequestLog>> GetLogs(DateTime? fromDate, DateTime? toDate);
    }
}
