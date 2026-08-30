using Microsoft.AspNetCore.Http;
using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface IRequestLogService
    {
        Task<Guid> LogRequest(HttpRequest request);
        Task LogResponse<T>(Guid logId, ApiResponse<T> response);
        Task LogResponse(Guid logId, object response);
        Task<IEnumerable<RequestLog>> GetLogs(DateTime? fromDate, DateTime? toDate);
    }
}
