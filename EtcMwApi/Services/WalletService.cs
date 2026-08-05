using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using EtcMwApi.Data;
using Microsoft.EntityFrameworkCore;

namespace EtcMwApi.Services
{
    public class WalletService : IWalletService
    {
        private readonly DatabaseContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<WalletService> _logger;

        public WalletService(
            DatabaseContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<WalletService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<IEnumerable<Wallet>> GetAllAsync()
        {
            return await _context.Wallets
                .Include(w => w.Customer)
                .Include(w => w.Vehicles)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Wallet?> GetByIdAsync(Guid id)
        {
            return await _context.Wallets
                .Include(w => w.Customer)
                .Include(w => w.Vehicles)
                .FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task<Wallet?> GetByWalletIdAsync(string walletNo)
        {
            return await _context.Wallets
                .Include(w => w.Customer)
                .Include(w => w.Vehicles)
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.WalletNo == walletNo);
        }

        public async Task<Wallet> AddAsync(Wallet wallet)
        {
            // ১. Guid ID খালি থাকলে তৈরি করা
            if (wallet.Id == Guid.Empty)
            {
                wallet.Id = Guid.NewGuid();
            }

            // ২. টাইমস্ট্যাম্প সেট করা
            var now = DateTime.UtcNow;
            wallet.CreatedAt = now;
            wallet.UpdatedAt = now;

            await _context.Wallets.AddAsync(wallet);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Wallet created successfully. WalletNo: {WalletNo}", wallet.WalletNo);

            return wallet;
        }

        public async Task UpdateAsync(Wallet wallet)
        {
            wallet.UpdatedAt = DateTime.UtcNow;

            _context.Wallets.Update(wallet);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Wallet updated successfully. ID: {Id}", wallet.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var wallet = await _context.Wallets.FindAsync(id);
            if (wallet != null)
            {
                _context.Wallets.Remove(wallet);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Wallet deleted successfully. ID: {Id}", id);
            }
            else
            {
                _logger.LogWarning("Delete failed. Wallet ID not found: {Id}", id);
            }
        }
    }
}