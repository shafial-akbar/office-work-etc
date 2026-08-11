using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using ETCGatewayAPI.Data;
using Microsoft.EntityFrameworkCore;
using Etc.Shared.Constants;

namespace ETCGatewayAPI.Services
{
    public class TranService : IDoTranService
    {
        private readonly DatabaseContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TranService> _logger;

        public TranService(DatabaseContext context,
                           IHttpContextAccessor httpContextAccessor,
                           ILogger<TranService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<IEnumerable<DoTransaction>> GetAllAsync()
        {
            return await _context.DoTransactions.ToListAsync();
        }

        public async Task<DoTransaction> GetByIdAsync(Guid id)
        {
            return await _context.DoTransactions.FindAsync(id);
        }

        // পার্টনার ট্রানজেকশন আইডি (PartnerTxnId) দিয়ে খোঁজার জন্য কাস্টম মেথড
        public async Task<DoTransaction?> GetByPartnerTxnIdAsync(string partnerTxnId)
        {
            return await _context.DoTransactions
                .FirstOrDefaultAsync(t => t.PartnerTxnId == partnerTxnId);
        }

        public async Task<Guid> AddAsync(DoTransaction transaction)
        {
            _context.DoTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return transaction.Id; // সেভ করার পর জেনারেটেড Id রিটার্ন করবে
        }

        public async Task UpdateAsync(DoTransaction transaction)
        {
            _context.DoTransactions.Update(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var transaction = await _context.DoTransactions.FindAsync(id);
            if (transaction != null)
            {
                _context.DoTransactions.Remove(transaction);
                await _context.SaveChangesAsync();
            }
        }
    }
}
