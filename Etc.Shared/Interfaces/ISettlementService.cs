using System;
using System.Threading.Tasks;
using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface ISettlementService
    {
        Task<DataprocessResponse> DoDataprocessAsync(DataprocessRequest request);
        Task<SettlementResponse> DoSettlementAsync(SettlementRequest request);
        Task<PostTransactionResponse> SendToCbsAsync(BatchRequestDto request);
    }    
}
