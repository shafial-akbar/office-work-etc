using System;
using System.Threading.Tasks;
using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface IReportService
    {
        Task<ReportResponse> GetReportAsync(ReportRequest request);
        Task<MasterBalanceSummaryResponse> GetMasterBalanceSummaryAsync(DateTime? fromDate, DateTime? toDate, DateTime? asOfDate = null);
    }    
}
