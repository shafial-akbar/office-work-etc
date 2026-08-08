using System;
using System.Threading.Tasks;
using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface ISettlementService
    {
        Task<SettlementReportResponse> GetSettlementReportAsync(SettlementReportRequest request);

    }    
}
