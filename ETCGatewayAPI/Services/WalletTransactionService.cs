using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using ETCGatewayAPI.Data;
using Microsoft.EntityFrameworkCore;
using Etc.Shared.Constants;
using Microsoft.AspNetCore.Http;

namespace ETCGatewayAPI.Services
{
    public class WalletTransactionService : IWalletTransactionService
    {

        private readonly DatabaseContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<WalletTransactionService> _logger;

        public WalletTransactionService(DatabaseContext context,
                           IHttpContextAccessor httpContextAccessor,
                           ILogger<WalletTransactionService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        // ১. ওয়ালেট টপ-আপ (Wallet Credit)
        public async Task<DoTransactionResponse> TopUpWalletAsync(DoTransactionRequest request)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();

            try
            {
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
                    BankTxnDate = DateTime.UtcNow,
                    TranStatus = TranStatus.Success,
                    SettlStatus = SettlementStatus.Pending,
                    ResponseCode = "200",
                    ResponseMessage = "Success"
                };

                await _context.DoTransactions.AddAsync(transaction);

                wallet.Balance += request.TransactionAmount;
                wallet.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                await _context.Entry(transaction).ReloadAsync();

                _logger.LogInformation("TopUp successful. BankTxnId: {BankTxnId}, New Balance: {Balance}", transaction.BankTxnId, wallet.Balance);

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

        // ২. টোল কালেকশন ও ব্যালেন্স কাটা (Toll Amount Debit/Deduction)
        public async Task<DoTransactionResponse> DeductTollAsync(DoTransactionRequest request)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.WalletNo == request.PartnerId && w.Status == WalletStatus.Active);

                if (wallet == null)
                {
                    _logger.LogWarning("Deduction Failed: Active wallet not found for PartnerId: {PartnerId}", request.PartnerId);
                    return new DoTransactionResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "Active wallet not found."
                    };
                }

                // পর্যাপ্ত ব্যালেন্স আছে কিনা চেক
                if (wallet.Balance < request.TransactionAmount)
                {
                    _logger.LogWarning("Deduction Failed: Insufficient balance for WalletNo: {PartnerId}. Current: {Balance}, Required: {Amount}",
                        request.PartnerId, wallet.Balance, request.TransactionAmount);

                    return new DoTransactionResponse
                    {
                        HttpCode = 400,
                        HttpStatus = "Bad Request",
                        Message = "Insufficient balance in wallet."
                    };
                }

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
                    TranMode = TranMode.Debit,
                    SourceChannel = request.SourceChannel,
                    BankTxnDate = DateTime.UtcNow,
                    TranStatus = TranStatus.Success,
                    SettlStatus = SettlementStatus.Pending,
                    ResponseCode = "200",
                    ResponseMessage = "Success"
                };

                await _context.DoTransactions.AddAsync(transaction);

                // ব্যালেন্স ডিডাক্ট
                wallet.Balance -= request.TransactionAmount;
                wallet.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                await _context.Entry(transaction).ReloadAsync();

                _logger.LogInformation("Toll Deduction successful. BankTxnId: {BankTxnId}, Remaining Balance: {Balance}", transaction.BankTxnId, wallet.Balance);

                return new DoTransactionResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = "Toll amount deducted successfully.",
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
                _logger.LogError(ex, "Error occurred during DeductToll for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                return new DoTransactionResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = "An error occurred while deducting toll amount."
                };
            }
        }

        // ৩. টোল ট্রানজেকশন রিভার্সাল বা রিফান্ড (Toll Amount Reversal/Credit)
        public async Task<DoTransactionResponse> ReverseTollAsync(DoTransactionRequest request)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // পূর্বে করা মূল ডেবিট ট্রানজেকশনটি যাচাই (RefNo1 বা PartnerTxnId দিয়ে)
                var originalTxn = await _context.DoTransactions
                    .FirstOrDefaultAsync(t => t.PartnerTxnId == request.PartnerTxnId && t.TranStatus == TranStatus.Success);

                if (originalTxn == null)
                {
                    _logger.LogWarning("Reversal Failed: Original transaction not found for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);
                    return new DoTransactionResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "Original transaction not found to reverse."
                    };
                }

                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.WalletNo == request.PartnerId && w.Status == WalletStatus.Active);

                if (wallet == null)
                {
                    _logger.LogWarning("Reversal Failed: Active wallet not found for PartnerId: {PartnerId}", request.PartnerId);
                    return new DoTransactionResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "Active wallet not found."
                    };
                }

                var reversalTransaction = new DoTransaction
                {
                    Id = Guid.NewGuid(),
                    PartnerId = request.PartnerId,
                    PartnerTxnId = $"REV_{request.PartnerTxnId}",
                    PartnerTransactionDate = request.PartnerTransactionDate,
                    SourceAccountNo = request.SourceAccountNo,
                    TransactionAmount = request.TransactionAmount,
                    RefNo1 = request.PartnerTxnId, // মূল ট্রানজেকশন রেফারেন্স
                    RefNo2 = request.RefNo2,
                    RefNo3 = request.RefNo3,
                    RefNo4 = request.RefNo4,
                    RefNo5 = request.RefNo5,
                    TranMode = TranMode.Credit, // রিভার্সালের ফলে ক্রেডিট হবে
                    SourceChannel = request.SourceChannel,
                    BankTxnDate = DateTime.UtcNow,
                    TranStatus = TranStatus.Success,
                    SettlStatus = SettlementStatus.Pending,
                    ResponseCode = "200",
                    ResponseMessage = "Reversal Success"
                };

                await _context.DoTransactions.AddAsync(reversalTransaction);

                // ওয়ালেটে ব্যালেন্স রিফান্ড/ক্রেডিট
                wallet.Balance += request.TransactionAmount;
                wallet.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                await _context.Entry(reversalTransaction).ReloadAsync();

                _logger.LogInformation("Reversal successful. BankTxnId: {BankTxnId}, Updated Balance: {Balance}", reversalTransaction.BankTxnId, wallet.Balance);

                return new DoTransactionResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = "Transaction reversed and balance refunded successfully.",
                    Body = new TransactionResultBody
                    {
                        BankTxnId = reversalTransaction.BankTxnId,
                        PartnerTxnId = reversalTransaction.PartnerTxnId,
                        TranStatus = reversalTransaction.TranStatus,
                        TransactionAmount = reversalTransaction.TransactionAmount
                    }
                };
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during Reversal for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                return new DoTransactionResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = "An error occurred while processing the transaction reversal."
                };
            }
        }

        // ৪. ওয়ালেটের বর্তমান ব্যালেন্স চেক
        public async Task<decimal> GetBalanceAsync(string walletNo)
        {
            try
            {
                var wallet = await _context.Wallets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.WalletNo == walletNo && w.Status == WalletStatus.Active);

                if (wallet == null)
                {
                    _logger.LogWarning("GetBalance Failed: Active wallet not found for WalletNo: {WalletNo}", walletNo);
                    throw new KeyNotFoundException($"Active wallet not found for WalletNo: {walletNo}");
                }

                return wallet.Balance;
            }
            catch (Exception ex) when (ex is not KeyNotFoundException)
            {
                _logger.LogError(ex, "Error occurred while fetching balance for WalletNo: {WalletNo}", walletNo);
                throw;
            }
        }
    }
}