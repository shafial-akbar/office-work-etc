using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface ITranService
    {
        // generic methods
        Task<IEnumerable<DoTransaction>> GetAllAsync();
        Task<DoTransaction> GetByIdAsync(Guid id);
        Task<DoTransaction> GetByPartnerTxnIdAsync(string partnerTxnId);
        Task<Guid> AddAsync(DoTransaction transaction);
        Task UpdateAsync(DoTransaction transaction);
        Task DeleteAsync(Guid id);

        // Pracmatic methods
        Task<DoTransactionResponse> TopUpWalletAsync(DoTransactionRequest request);
    }
}
