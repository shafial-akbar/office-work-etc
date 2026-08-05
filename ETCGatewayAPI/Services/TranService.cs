using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using ETCGatewayAPI.Data;
using Microsoft.EntityFrameworkCore;
using Etc.Shared.Constants;

namespace ETCGatewayAPI.Services
{
    public class TranService : ITranService
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

        // ==========================================
        // BUSINESS LOGIC / WALLET TOP-UP METHOD
        // ==========================================
        public async Task<DoTransactionResponse> TopUpWalletAsync(DoTransactionRequest request)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ১. ওয়ালেট ভ্যালিডেশন এবং ফিচ করা
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.WalletNo == request.PartnerId && w.Status == WalletStatus.Active);

                if (wallet == null)
                {
                    _logger.LogWarning("TopUp Failed: Active wallet not found for WalletNo/PartnerId: {PartnerId}", request.PartnerId);
                    return new DoTransactionResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "Active wallet record not found for the provided PartnerId."
                    };
                }

                // ২. নতুন ডু-ট্রানজেকশন এন্টিটি ম্যাপ করা (Using Constants)
                var transaction = new DoTransaction
                {
                    Id = Guid.NewGuid(),
                    PartnerId = request.PartnerId,
                    PartnerTxnId = request.PartnerTxnId,
                    PartnerTransactionDate = request.PartnerTransactionDate,
                    SourceAccountNo = request.SourceAccountNo,
                    TransactionAmount = request.TransactionAmount,
                    RefNo1 = request.RefNo1,
                    RefNo2 = request.RefNo2,
                    RefNo3 = request.RefNo3,
                    RefNo4 = request.RefNo4,
                    RefNo5 = request.RefNo5,
                    TranMode = TranMode.Credit,
                    SourceChannel = request.SourceChannel,

                    // টাইপ-সেফ কনস্ট্যান্টস ব্যবহার
                    BankTxnDate = DateTime.UtcNow,
                    TranStatus = TranStatus.Success,
                    SettlStatus = SettlementStatus.Pending,
                    ResponseCode = "200",
                    ResponseMessage = "Success"
                };

                await _context.DoTransactions.AddAsync(transaction);

                wallet.Balance += request.TransactionAmount;
                wallet.UpdatedAt = DateTime.UtcNow;

                // ৪. ডাটাবেজ চেঞ্জ সেভ ও ট্রানজেকশন কমিট করা
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // সিকোয়েন্স/ট্রিগার আইডি আপডেট করতে এন্ট্রি রি-লোড
                await _context.Entry(transaction).ReloadAsync();

                _logger.LogInformation("TopUp successful. BankTxnId: {BankTxnId}, New Balance: {Balance}", transaction.BankTxnId, wallet.Balance);

                // ৫. সফল রেসপন্স অবজেক্ট তৈরি
                return new DoTransactionResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = "Transaction processed and wallet updated successfully.",
                    Body = new TransactionResultBody
                    {
                        BankTxnId = transaction.BankTxnId,
                        PartnerTxnId = transaction.PartnerTxnId,
                        TranStatus = transaction.TranStatus,
                        TransactionAmount = transaction.TransactionAmount
                    }
                };
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during TopUpWallet for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                return new DoTransactionResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = "An error occurred while processing the wallet transaction."
                };
            }
        }

    }
}
