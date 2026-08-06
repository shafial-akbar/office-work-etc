using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using ETCGatewayAPI.Data;
using Microsoft.EntityFrameworkCore;
using Etc.Shared.Constants;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Razor.TagHelpers;

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
            var requestTime = DateTime.UtcNow;

            try
            {
                // ১. ওয়ালেট ভ্যালিডেশন
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.WalletNo == request.PartnerId && w.Status == WalletStatus.Active);

                if (wallet == null)
                {
                    _logger.LogWarning("TopUp Failed: Active wallet not found for WalletNo/PartnerId: {PartnerId}", request.PartnerId);

                    var notFoundResponse = new DoTransactionResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "Active wallet record not found for the provided PartnerId."
                    };

                    await SaveTransactionLogAsync(
                        request: request,
                        response: notFoundResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TopUp,
                        tranMode: TranMode.Credit
                    );

                    return notFoundResponse;
                }

                // অডিটের জন্য ট্রানজেকশনের আগের ব্যালেন্স সংরক্ষণ
                decimal balanceBefore = wallet.Balance;

                // ২. ট্রানজেকশন এন্ট্রি তৈরি
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

                // ৩. ওয়ালেট ব্যালেন্স আপডেট
                wallet.Balance += request.TransactionAmount;
                wallet.UpdatedAt = DateTime.UtcNow;

                decimal balanceAfter = wallet.Balance;

                // ৪. সফল রেসপন্স অবজেক্ট তৈরি
                var successResponse = new DoTransactionResponse
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

                // ৫. SaveTransactionLogAsync কল করা (ডাইনামিক requestType ও tranMode সহ)
                await SaveTransactionLogAsync(
                    request: request,
                    response: successResponse,
                    requestTime: requestTime,
                    status: "Success",
                    requestType: TranLogRequestType.TopUp,
                    tranMode: TranMode.Credit,
                    sblTxnId: transaction.BankTxnId,
                    balanceBefore: balanceBefore,
                    balanceAfter: balanceAfter
                );

                // ৬. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                await _context.Entry(transaction).ReloadAsync();

                _logger.LogInformation("TopUp successful. BankTxnId: {BankTxnId}, New Balance: {Balance}", transaction.BankTxnId, wallet.Balance);

                return successResponse;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during TopUpWallet for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                var errorResponse = new DoTransactionResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = "An error occurred while processing the wallet transaction."
                };

                await SaveTransactionLogAsync(
                    request: request,
                    response: errorResponse,
                    requestTime: requestTime,
                    status: "Failed",
                    requestType: TranLogRequestType.TopUp,
                    tranMode: TranMode.Credit,
                    errorMessage: ex.Message
                );

                return errorResponse;
            }
        }

        // ২. টোল কালেকশন ও ব্যালেন্স কাটা (Toll Amount Debit/Deduction)
        public async Task<DoTransactionResponse> DeductTollAsync(DoTransactionRequest request)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            var requestTime = DateTime.UtcNow;

            try
            {
                // ১. ওয়ালেট ভ্যালিডেশন
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.WalletNo == request.PartnerId && w.Status == WalletStatus.Active);

                if (wallet == null)
                {
                    _logger.LogWarning("Deduction Failed: Active wallet not found for PartnerId: {PartnerId}", request.PartnerId);

                    var notFoundResponse = new DoTransactionResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "Active wallet not found."
                    };

                    await SaveTransactionLogAsync(
                        request: request,
                        response: notFoundResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TollDeduction,
                        tranMode: TranMode.Debit
                    );

                    return notFoundResponse;
                }

                // অডিটের জন্য ট্রানজেকশনের আগের ব্যালেন্স সংরক্ষণ
                decimal balanceBefore = wallet.Balance;

                // পর্যাপ্ত ব্যালেন্স আছে কিনা চেক
                if (wallet.Balance < request.TransactionAmount)
                {
                    _logger.LogWarning("Deduction Failed: Insufficient balance for WalletNo: {PartnerId}. Current: {Balance}, Required: {Amount}",
                        request.PartnerId, wallet.Balance, request.TransactionAmount);

                    var insufficientBalanceResponse = new DoTransactionResponse
                    {
                        HttpCode = 400,
                        HttpStatus = "Bad Request",
                        Message = "Insufficient balance in wallet."
                    };

                    await SaveTransactionLogAsync(
                        request: request,
                        response: insufficientBalanceResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TollDeduction,
                        tranMode: TranMode.Debit,
                        balanceBefore: balanceBefore
                    );

                    return insufficientBalanceResponse;
                }

                // ২. ট্রানজেকশন এন্ট্রি তৈরি
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

                // ৩. ব্যালেন্স ডিডাক্ট
                wallet.Balance -= request.TransactionAmount;
                wallet.UpdatedAt = DateTime.UtcNow;

                decimal balanceAfter = wallet.Balance;

                // ৪. সফল রেসপন্স অবজেক্ট তৈরি
                var successResponse = new DoTransactionResponse
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

                // ৫. SaveTransactionLogAsync কল করা (Deduction & Debit সহ)
                await SaveTransactionLogAsync(
                    request: request,
                    response: successResponse,
                    requestTime: requestTime,
                    status: "Success",
                    requestType: TranLogRequestType.TollDeduction,
                    tranMode: TranMode.Debit,
                    sblTxnId: transaction.BankTxnId,
                    balanceBefore: balanceBefore,
                    balanceAfter: balanceAfter
                );

                // ৬. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                await _context.Entry(transaction).ReloadAsync();

                _logger.LogInformation("Toll Deduction successful. BankTxnId: {BankTxnId}, Remaining Balance: {Balance}", transaction.BankTxnId, wallet.Balance);

                return successResponse;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during DeductToll for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                var errorResponse = new DoTransactionResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = "An error occurred while deducting toll amount."
                };

                await SaveTransactionLogAsync(
                    request: request,
                    response: errorResponse,
                    requestTime: requestTime,
                    status: "Failed",
                    requestType: TranLogRequestType.TollDeduction,
                    tranMode: TranMode.Debit,
                    errorMessage: ex.Message
                );

                return errorResponse;
            }
        }

        // ৩. টোল ট্রানজেকশন রিভার্সাল বা রিফান্ড (Toll Amount Reversal/Credit)
        public async Task<DoTransactionResponse> ReverseTollAsync(DoTransactionRequest request)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            var requestTime = DateTime.UtcNow;

            try
            {
                // ১. পূর্বে করা মূল ট্রানজেকশনটি যাচাই (PartnerTxnId দিয়ে)
                var originalTxn = await _context.DoTransactions
                    .FirstOrDefaultAsync(t => t.PartnerTxnId == request.PartnerTxnId && t.TranStatus == TranStatus.Success);

                if (originalTxn == null)
                {
                    _logger.LogWarning("Reversal Failed: Original transaction not found for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                    var notFoundTxnResponse = new DoTransactionResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "Original transaction not found to reverse."
                    };

                    await SaveTransactionLogAsync(
                        request: request,
                        response: notFoundTxnResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TollReverse,
                        tranMode: TranMode.Credit
                    );

                    return notFoundTxnResponse;
                }

                // ২. ওয়ালেট ভ্যালিডেশন
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.WalletNo == request.PartnerId && w.Status == WalletStatus.Active);

                if (wallet == null)
                {
                    _logger.LogWarning("Reversal Failed: Active wallet not found for PartnerId: {PartnerId}", request.PartnerId);

                    var notFoundWalletResponse = new DoTransactionResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "Active wallet not found."
                    };

                    await SaveTransactionLogAsync(
                        request: request,
                        response: notFoundWalletResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TollReverse,
                        tranMode: TranMode.Credit
                    );

                    return notFoundWalletResponse;
                }

                // অডিটের জন্য ট্রানজেকশনের আগের ব্যালেন্স সংরক্ষণ
                decimal balanceBefore = wallet.Balance;

                // ৩. রিভার্সাল ট্রানজেকশন এন্ট্রি তৈরি
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

                // ৪. ওয়ালেটে ব্যালেন্স রিফান্ড/ক্রেডিট
                wallet.Balance += request.TransactionAmount;
                wallet.UpdatedAt = DateTime.UtcNow;

                decimal balanceAfter = wallet.Balance;

                // ৫. সফল রেসপন্স অবজেক্ট তৈরি
                var successResponse = new DoTransactionResponse
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

                // ৬. SaveTransactionLogAsync কল করা (TollReverse & Credit সহ)
                await SaveTransactionLogAsync(
                    request: request,
                    response: successResponse,
                    requestTime: requestTime,
                    status: "Success",
                    requestType: TranLogRequestType.TollReverse,
                    tranMode: TranMode.Credit,
                    sblTxnId: reversalTransaction.BankTxnId,
                    balanceBefore: balanceBefore,
                    balanceAfter: balanceAfter
                );

                // ৭. ডাটাবেজ সেভ ও ট্রানজেকশন কমিট
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                await _context.Entry(reversalTransaction).ReloadAsync();

                _logger.LogInformation("Reversal successful. BankTxnId: {BankTxnId}, Updated Balance: {Balance}", reversalTransaction.BankTxnId, wallet.Balance);

                return successResponse;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during Reversal for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                var errorResponse = new DoTransactionResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = "An error occurred while processing the transaction reversal."
                };

                await SaveTransactionLogAsync(
                    request: request,
                    response: errorResponse,
                    requestTime: requestTime,
                    status: "Failed",
                    requestType: TranLogRequestType.TollReverse,
                    tranMode: TranMode.Credit,
                    errorMessage: ex.Message
                );

                return errorResponse;
            }
        }

        // ৪. ওয়ালেটের বর্তমান ব্যালেন্স চেক (check by Mobile No or Wallet No)
        public async Task<List<WalletBalanceResultDto>> GetWalletBalanceAsync(string searchKey)
        {
            var requestTime = DateTime.UtcNow;

            // ১. খালি/ইনভ্যালিড সার্চ কি-এর ক্ষেত্রে অডিট লগ ও রেসপন্স
            if (string.IsNullOrWhiteSpace(searchKey))
            {
                var emptyResponse = new List<WalletBalanceResultDto>();

                await SaveInquiryTransactionLogAsync(
                    searchKey: searchKey,
                    response: emptyResponse,
                    requestTime: requestTime,
                    status: "Failed",
                    errorMessage: "Search key cannot be null or empty."
                );

                return emptyResponse;
            }

            try
            {
                // ২. রিড-ওনলি ডাটাবেজ কোয়েরি
                var result = await _context.Wallets
                    .AsNoTracking()
                    .Where(w => w.Status == WalletStatus.Active &&
                               (w.MobileNo == searchKey || w.WalletNo == searchKey))
                    .Select(w => new WalletBalanceResultDto
                    {
                        WalletNo = w.WalletNo,
                        Balance = w.Balance,
                        Currency = w.Currency
                    })
                    .ToListAsync();

                // ৩. ইনকোয়ারি সফল হলে TransactionLog সেভ
                await SaveInquiryTransactionLogAsync(
                    searchKey: searchKey,
                    response: result,
                    requestTime: requestTime,
                    status: "Success",
                    accountNo: result.FirstOrDefault()?.WalletNo
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during GetWalletBalanceAsync for searchKey: {SearchKey}", searchKey);

                var errorResponse = new List<WalletBalanceResultDto>();

                await SaveInquiryTransactionLogAsync(
                    searchKey: searchKey,
                    response: errorResponse,
                    requestTime: requestTime,
                    status: "Failed",
                    errorMessage: ex.Message
                );

                return errorResponse;
            }
        }

        // 5. ট্রানজেকশন রিকনসিলিয়েশন (Reconciliation & Idempotency Audit)
        public async Task<ReconcileTransactionResponse> ReconcileTransactionAsync(ReconcileTransactionRequest request)
        {
            var requestTime = DateTime.UtcNow;

            // ১. ভ্যালিডেশন
            if (string.IsNullOrWhiteSpace(request.PartnerTxnId))
            {
                return new ReconcileTransactionResponse
                {
                    HttpCode = 400,
                    HttpStatus = "Bad Request",
                    Message = "PartnerTxnId cannot be null or empty."
                };
            }

            try
            {
                // ২. Primary Check: DoTransactions টেবিলে সফল বা প্রসেসড রেকর্ড খোঁজা
                var transaction = await _context.DoTransactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.PartnerTxnId == request.PartnerTxnId);

                // Case : ডাটাবেজে সফলভাবে এক্সিকিউট ও সেভ হয়েছিল (Response Lost / Drop Case)
                if (transaction != null)
                {
                    var successReconResponse = new ReconcileTransactionResponse
                    {
                        HttpCode = 200,
                        HttpStatus = "OK",
                        Message = "Transaction record found in primary database.",
                        Body = new ReconcileResultBody
                        {
                            PartnerTxnId = transaction.PartnerTxnId,
                            BankTxnId = transaction.BankTxnId,
                            ReconStatus = transaction.TranStatus.Equals(TranStatus.Success, StringComparison.OrdinalIgnoreCase)
                                ? TranStatus.Success
                                : TranStatus.Failed,
                            TransactionAmount = transaction.TransactionAmount,
                            PartnerId = transaction.PartnerId,
                            SourceAccountNo = transaction.SourceAccountNo,
                            TranMode = transaction.TranMode,
                            TransactionDate = transaction.BankTxnDate
                        }
                    };

                    // আপনার বিদ্যমান SaveTransactionLogAsync ব্যবহার করে অডিট লগ সেভ
                    await SaveTransactionLogAsync(
                        request: new DoTransactionRequest
                        {
                            PartnerId = transaction.PartnerId,
                            PartnerTxnId = transaction.PartnerTxnId,
                            SourceAccountNo = transaction.SourceAccountNo,
                            TransactionAmount = transaction.TransactionAmount
                        },
                        response: new DoTransactionResponse
                        {
                            HttpCode = successReconResponse.HttpCode,
                            HttpStatus = successReconResponse.HttpStatus,
                            Message = successReconResponse.Message
                        },
                        requestTime: requestTime,
                        status : "Success",
                        requestType: TranLogRequestType.Reconcile,
                        tranMode: transaction.TranMode,
                        sblTxnId: transaction.PartnerId
                    );

                    return successReconResponse;
                }

                // ৩. Secondary Check: TransactionLogs টেবিলে টেকনিক্যাল ফেলিউর রেকর্ড চেক (Case 3)
                var failedLog = await _context.TransactionLogs
                    .AsNoTracking()
                    .Where(l => l.PartnerTxnId == request.PartnerTxnId)
                    .OrderByDescending(l => l.RequestTimestamp)
                    .FirstOrDefaultAsync();

                // Case : রিকোয়েস্ট API-তে এসেছিল কিন্তু ভ্যালিডেশন/ডাটাবেজ এররে ফেল করেছিল
                if (failedLog != null)
                {
                    var failedReconResponse = new ReconcileTransactionResponse
                    {
                        HttpCode = 200,
                        HttpStatus = "OK",
                        Message = "Transaction attempted but failed during execution.",
                        Body = new ReconcileResultBody
                        {
                            PartnerTxnId = request.PartnerTxnId,
                            BankTxnId = failedLog.SblTxnId ?? string.Empty,
                            ReconStatus = TranStatus.Failed,
                            TransactionAmount = failedLog.TransactionAmount ?? 0,
                            PartnerId = failedLog.PartnerId ?? request.PartnerId ?? string.Empty,
                            SourceAccountNo = failedLog.AccountNo ?? string.Empty,
                            TranMode = failedLog.TranMode ?? string.Empty,
                            TransactionDate = failedLog.RequestTimestamp
                        }
                    };

                    await SaveTransactionLogAsync(
                        request: new DoTransactionRequest
                        {
                            PartnerId = failedLog.PartnerId ?? request.PartnerId ?? string.Empty,
                            PartnerTxnId = request.PartnerTxnId,
                            SourceAccountNo = failedLog.AccountNo ?? string.Empty,
                            TransactionAmount = failedLog.TransactionAmount ?? 0
                        },
                        response: new DoTransactionResponse
                        {
                            HttpCode = failedReconResponse.HttpCode,
                            HttpStatus = failedReconResponse.HttpStatus,
                            Message = failedReconResponse.Message
                        },
                        requestTime: requestTime,
                        status: "Success",
                        requestType: TranLogRequestType.Reconcile,
                        tranMode: transaction.TranMode,
                        sblTxnId: transaction.PartnerId
                    );

                    return failedReconResponse;
                }

                // Case : নেটওয়ার্ক সমস্যার কারণে রিকোয়েস্ট API পর্যন্ত পৌঁছায়নি (Not Found)
                var notFoundResponse = new ReconcileTransactionResponse
                {
                    HttpCode = 404,
                    HttpStatus = "Not Found",
                    Message = "No transaction record found with the provided PartnerTxnId.",
                    Body = new ReconcileResultBody
                    {
                        PartnerTxnId = request.PartnerTxnId,
                        BankTxnId = string.Empty,
                        ReconStatus = "NOT_FOUND",
                        TransactionAmount = 0,
                        PartnerId = request.PartnerId ?? string.Empty
                    }
                };

                await SaveTransactionLogAsync(
                    request: new DoTransactionRequest
                    {
                        PartnerId = request.PartnerId ?? string.Empty,
                        PartnerTxnId = request.PartnerTxnId
                    },
                    response: new DoTransactionResponse
                    {
                        HttpCode = notFoundResponse.HttpCode,
                        HttpStatus = notFoundResponse.HttpStatus,
                        Message = notFoundResponse.Message
                    },
                    requestTime: requestTime,
                    status : "Failed",
                    requestType: TranLogRequestType.Reconcile,
                    tranMode: string.Empty
                );

                return notFoundResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Reconciliation for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                var errorResponse = new ReconcileTransactionResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = "An error occurred while handling the reconciliation request."
                };

                await SaveTransactionLogAsync(
                    request: new DoTransactionRequest
                    {
                        PartnerId = request.PartnerId ?? string.Empty,
                        PartnerTxnId = request.PartnerTxnId
                    },
                    response: new DoTransactionResponse
                    {
                        HttpCode = errorResponse.HttpCode,
                        HttpStatus = errorResponse.HttpStatus,
                        Message = errorResponse.Message
                    },
                    requestTime: requestTime,
                    status : "Failed",
                    requestType: TranLogRequestType.Reconcile,
                    tranMode: string.Empty,
                    errorMessage: ex.Message
                );

                return errorResponse;
            }
        }

        private async Task SaveTransactionLogAsync(
            DoTransactionRequest request,
            DoTransactionResponse response,
            DateTime requestTime,
            string status,
            string requestType,
            string tranMode = null,
            string sblTxnId = null,
            decimal? balanceBefore = null,
            decimal? balanceAfter = null,
            string errorMessage = null)
        {
            try
            {
                var log = new TransactionLog
                {
                    Id = Guid.NewGuid(),
                    PartnerId = request?.PartnerId,
                    PartnerTxnId = request?.PartnerTxnId,
                    RequestType = requestType,
                    RequestData = request != null ? JsonSerializer.Serialize(request) : null,
                    ResponseData = response != null ? JsonSerializer.Serialize(response) : null,
                    ResponseCode = response?.HttpCode.ToString(),
                    ResponseMessage = errorMessage ?? response?.Message,
                    RequestTimestamp = requestTime,
                    ResponseTimestamp = DateTime.UtcNow,
                    Status = status,
                    SblTxnId = sblTxnId,
                    AccountNo = request?.SourceAccountNo,
                    TransactionAmount = request?.TransactionAmount,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    TranMode = tranMode
                };

                await _context.TransactionLogs.AddAsync(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write TransactionLog for PartnerTxnId: {PartnerTxnId}", request?.PartnerTxnId);
            }
        }

        // AccountInquiry-এর জন্য কাস্টমাইজড হেলপার মেথড
        private async Task SaveInquiryTransactionLogAsync(
            string searchKey,
            object response,
            DateTime requestTime,
            string status,
            string accountNo = null,
            string errorMessage = null)
        {
            try
            {
                var log = new TransactionLog
                {
                    Id = Guid.NewGuid(),
                    PartnerId = null,
                    PartnerTxnId = null,
                    RequestType = TranLogRequestType.AccountInquiry,
                    RequestData = JsonSerializer.Serialize(new { SearchKey = searchKey }),
                    ResponseData = response != null ? JsonSerializer.Serialize(response) : null,
                    ResponseCode = status == "Success" ? "200" : "400",
                    ResponseMessage = errorMessage ?? (status == "Success" ? "Inquiry Successful" : "Inquiry Failed"),
                    RequestTimestamp = requestTime,
                    ResponseTimestamp = DateTime.UtcNow,
                    Status = status,
                    SblTxnId = null,
                    AccountNo = accountNo ?? searchKey,
                    TransactionAmount = null,
                    BalanceBefore = null,
                    BalanceAfter = null,
                    TranMode = null
                };

                await _context.TransactionLogs.AddAsync(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write Inquiry TransactionLog for searchKey: {SearchKey}", searchKey);
            }
        }
    }
}