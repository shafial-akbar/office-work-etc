using Etc.Shared.Interfaces;
using Etc.Shared.Models;
using Etc.Shared.DTOs;
using Etc.Shared.Helpers;
using ETCGatewayAPI.Data;
using Microsoft.EntityFrameworkCore;
using Etc.Shared.Constants;
using System.Text.Json;

namespace ETCGatewayAPI.Services
{
    public class WalletTransactionService : IWalletTransactionService
    {

        // ১. প্রতিটি ফিল্ড শুধুমাত্র একবারই থাকবে
        private readonly DatabaseContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<WalletTransactionService> _logger;

        // ২. কনস্ট্রাক্টর ইনিশিয়ালাইজেশন
        public WalletTransactionService(
            DatabaseContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<WalletTransactionService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        // ১. ওয়ালেট টপ-আপ (Wallet Credit)
        public async Task<TopUpResponse> TopUpWalletAsync(TopUpRequest request)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            var requestTime = DateTime.UtcNow;

            try
            {
                // 1.1 ওয়ালেট ভ্যালিডেশন
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.WalletNo == request.WalletNo && w.Status == WalletStatus.Active);

                if (wallet == null)
                {
                    _logger.LogWarning("TopUp Failed: Active wallet not found for WalletNo: {WalletNo}", request.WalletNo);

                    var notFoundResponse = new TopUpResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "Active wallet record not found for the provided WalletNo."
                    };

                    await SaveTransactionLogAsync(
                        topUpRequest: request,
                        topUpResponse: notFoundResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TopUp,
                        tranMode: TranMode.Credit
                    );

                    return notFoundResponse;
                }

                // 1.2 RefNo1 Uuplicacy Check
                bool isDuplicateRef = await _context.DoTransactions.AnyAsync(t => t.RefNo1 == request.ReferenceId);

                if (isDuplicateRef)
                {
                    _logger.LogWarning("TopUp Failed: Duplicate ReferenceId: {ReferenceId}", request.ReferenceId);

                    var duplicateResponse = new TopUpResponse
                    {
                        HttpCode = 400, // অথবা 409 (Conflict)
                        HttpStatus = "Bad Request",
                        Message = "The provided ReferenceId already exists."
                    };

                    await SaveTransactionLogAsync(
                        topUpRequest: request,
                        topUpResponse: duplicateResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TopUp,
                        tranMode: TranMode.Credit,
                        errorMessage: "Duplicate ReferenceId"
                    );

                    return duplicateResponse;
                }

                // 1.3 Topup todayCount
                var todayCount = await _context.DoTransactions.CountAsync(t => t.WalletId == wallet.Id
                  && t.BankTxnDate.Date == DateTime.UtcNow.Date
                  && t.TranStatus == TranStatus.Success);

                if (todayCount >= TopUpLimits.MaxDailyTopUpCount)
                {
                    return new TopUpResponse
                    {
                        HttpCode = 400,
                        HttpStatus = "Bad Request",
                        Message = $"Daily top-up limit reached. Maximum allowed count is {TopUpLimits.MaxDailyTopUpCount} per day."
                    };
                }

                // অডিটের জন্য ট্রানজেকশনের আগের ব্যালেন্স সংরক্ষণ
                decimal balanceBefore = wallet.Balance;

                // ২. ট্রানজেকশন এন্ট্রি তৈরি
                var transaction = new DoTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    SourceAccountNo = request.SourceAccountNo,
                    TransactionAmount = request.TransactionAmount,
                    RefNo1 = request.ReferenceId,
                    ChannelTransactionDate = DateTimeHelper.ParseToDateTime(request.ChannelTransactionDate),
                    TranMode = TranMode.Credit,
                    SourceChannel = request.SourceChannel,
                    BankTxnId = $"{DateTime.UtcNow:yyMMddHHmmss}{new Random().Next(100000, 999999)}",
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
                var successResponse = new TopUpResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = "Transaction processed and wallet TopUp successfully.",
                    Body = new TopUpResultBody
                    {
                        BankTxnId = transaction.BankTxnId,
                        WalletNo = wallet.WalletNo,
                        NewBalance = wallet.Balance,
                        TransactionAmount = transaction.TransactionAmount,
                        TranStatus = transaction.TranStatus,
                        MobileNo = wallet.MobileNo,
                    }
                };

                // ৫. SaveTransactionLogAsync কল করা (ডাইনামিক requestType ও tranMode সহ)
                await SaveTransactionLogAsync(
                    topUpRequest: request,
                    topUpResponse: successResponse,
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

                _logger.LogInformation("TopUp successful. BankTxnId: {BankTxnId}, RefId: {RefNo1}, New Balance: {Balance}", transaction.BankTxnId, transaction.RefNo1, wallet.Balance);

                return successResponse;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during TopUpWallet for WalletNo: {WalletNo}, RefId: {RefNo1}", request.WalletNo, request.ReferenceId);

                var errorResponse = new TopUpResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = "An error occurred while processing the wallet TopUp transaction."
                };

                await SaveTransactionLogAsync(
                    topUpRequest: request,
                    topUpResponse: errorResponse,
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
                    PartnerTransactionDate = DateTimeHelper.ParseToDateTime(request.PartnerTransactionDate),
                    SourceAccountNo = request.SourceAccountNo,
                    TransactionAmount = request.TransactionAmount,
                    RefNo1 = request.RefNo1,
                    RefNo2 = request.RefNo2,
                    RefNo3 = request.RefNo3,
                    RefNo4 = request.RefNo4,
                    RefNo5 = request.RefNo5,
                    TranMode = TranMode.Debit,
                    BankTxnId = $"{DateTime.UtcNow:yyMMddHHmmss}{new Random().Next(100000, 999999)}",
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
        public async Task<DoTransactionResponse> ReverseTollAsync(ReverseTransactionRequest request)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            var requestTime = DateTime.UtcNow;

            try
            {
                // ১. পূর্বে করা মূল ট্রানজেকশনটি যাচাই (PartnerTxnId, PartnerId, Amount এবং Success স্ট্যাটাস দিয়ে)
                var originalTxn = await _context.DoTransactions
                    .FirstOrDefaultAsync(t => t.PartnerTxnId == request.PartnerTxnId
                                           && t.PartnerId == request.PartnerId
                                           && t.TransactionAmount == request.TransactionAmount
                                           && t.TranStatus == TranStatus.Success);

                if (originalTxn == null)
                {
                    _logger.LogWarning("Reversal Failed: Original successful transaction not found for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                    var notFoundTxnResponse = new DoTransactionResponse
                    {
                        HttpCode = 404,
                        HttpStatus = "Not Found",
                        Message = "Original transaction not found to reverse."
                    };

                    await SaveTransactionLogAsync(
                        reverseRequest: request,
                        response: notFoundTxnResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TollReverse,
                        tranMode: TranMode.Credit
                    );

                    return notFoundTxnResponse;
                }

                // ২. ট্রানজেকশনটি ইতোমধ্যে EOD Settlement-এ প্রসেস হয়ে গেছে কিনা তা যাচাই
                if (originalTxn.SettlStatus == SettlementStatus.Settled || originalTxn.SettlStatus == SettlementStatus.Processing)
                {
                    _logger.LogWarning("Reversal Failed: Transaction already settled for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                    var settledResponse = new DoTransactionResponse
                    {
                        HttpCode = 400,
                        HttpStatus = "Bad Request",
                        Message = "Cannot reverse a transaction that has already been settled."
                    };

                    await SaveTransactionLogAsync(
                        reverseRequest: request,
                        response: settledResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TollReverse,
                        tranMode: TranMode.Credit,
                        sblTxnId: originalTxn.BankTxnId
                    );

                    return settledResponse;
                }

                // ৩. ওয়ালেট ভ্যালিডেশন
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
                        reverseRequest: request,
                        response: notFoundWalletResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TollReverse,
                        tranMode: TranMode.Credit
                    );

                    return notFoundWalletResponse;
                }

                // ৪. মূল ট্রানজেকশনটি ইতোমধ্যে রিভার্সড কিনা চেক করা
                if (originalTxn.TranStatus == TranStatus.Reversed)
                {
                    _logger.LogWarning("Reversal Failed: Transaction already reversed for PartnerTxnId: {PartnerTxnId}", request.PartnerTxnId);

                    var alreadyReversedResponse = new DoTransactionResponse
                    {
                        HttpCode = 409,
                        HttpStatus = "Conflict",
                        Message = "Transaction has already been reversed."
                    };

                    await SaveTransactionLogAsync(
                        reverseRequest: request,
                        response: alreadyReversedResponse,
                        requestTime: requestTime,
                        status: "Failed",
                        requestType: TranLogRequestType.TollReverse,
                        tranMode: TranMode.Credit,
                        sblTxnId: originalTxn.BankTxnId
                    );

                    return alreadyReversedResponse;
                }

                // অডিটের জন্য ওয়ালেটের আগের ব্যালেন্স সংরক্ষণ
                decimal balanceBefore = wallet.Balance;

                // ৫. মূল ট্রানজেকশনটির স্ট্যাটাস Reversed এ আপডেট করা
                originalTxn.TranStatus = TranStatus.Reversed;
                originalTxn.ResponseMessage = "Transaction Reversed";

                // ৬. নতুন Reversal Transaction আইডি তৈরি (অন্যান্য মেথডের সেম ফরম্যাট)
                string newReversalBankTxnId = $"{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100000, 999999)}_REV";

                // ৭. DoTransactions টেবিলে নতুন Credit Reversal এন্ট্রি তৈরি করা
                var newReversalTxn = new DoTransaction
                {
                    Id = Guid.NewGuid(),
                    PartnerId = request.PartnerId,
                    PartnerTxnId = $"{request.PartnerTxnId}_REV",
                    OriginalBankTxnId = originalTxn.BankTxnId,
                    TransactionAmount = request.TransactionAmount,
                    TranMode = TranMode.Credit,
                    BankTxnId = newReversalBankTxnId,
                    BankTxnDate = requestTime,
                    TranStatus = TranStatus.Success,
                    SettlStatus = SettlementStatus.Pending, // Pending রাখার অর্থ হলো— "এই রিভার্সাল বা ক্রেডিটের হিসাবটি এখনও EOD Settlement Batch-এ প্রসেস করা বাকি আছে। দিনশেষে সেটেলমেন্ট সামারি তৈরির সময় এই ক্রেডিট অ্যামাউন্টটি টোল কর্তৃপক্ষের প্রাপ্য টাকা থেকে বিয়োগ করতে হবে।
                    ResponseCode = "200",
                    ResponseMessage = "Toll Reversal Credit",
                };

                await _context.DoTransactions.AddAsync(newReversalTxn);

                // ৮. ওয়ালেটে ব্যালেন্স রিফান্ড/ক্রেডিট
                wallet.Balance += request.TransactionAmount;
                wallet.UpdatedAt = DateTime.UtcNow;

                decimal balanceAfter = wallet.Balance;

                // ৯. সফল রেসপন্স অবজেক্ট তৈরি
                var successResponse = new DoTransactionResponse
                {
                    HttpCode = 200,
                    HttpStatus = "OK",
                    Message = "Transaction reversed and balance refunded successfully.",
                    Body = new TransactionResultBody
                    {
                        BankTxnId = newReversalBankTxnId,
                        PartnerTxnId = originalTxn.PartnerTxnId,
                        TranStatus = newReversalTxn.TranStatus,
                        TransactionAmount = newReversalTxn.TransactionAmount
                    }
                };

                // ১০. Log & Database Save
                await SaveTransactionLogAsync(
                    reverseRequest: request,
                    response: successResponse,
                    requestTime: requestTime,
                    status: "Success",
                    requestType: TranLogRequestType.TollReverse,
                    tranMode: TranMode.Credit,
                    sblTxnId: newReversalBankTxnId,
                    balanceBefore: balanceBefore,
                    balanceAfter: balanceAfter
                );

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation("Reversal successful. New Reversal TxnId: {ReversalTxnId}, Original TxnId: {OriginalTxnId}, Updated Balance: {Balance}",
                    newReversalBankTxnId, originalTxn.BankTxnId, wallet.Balance);

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
                    reverseRequest: request,
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

        // 5. ট্রানজেকশন রিকনসিলিয়েশন (Reconciliation & Idempotency Audit)
        public async Task<ReconcileTransactionResponse> ReconcileTransactionAsync(ReconcileTransactionRequest request)
        {
            var requestTime = DateTime.UtcNow;

            // ১. প্রাথমিক ভ্যালিডেশন
            if (string.IsNullOrWhiteSpace(request.PartnerTxnId) && string.IsNullOrWhiteSpace(request.ReferenceId))
            {
                var badRequestResponse = new ReconcileTransactionResponse
                {
                    HttpCode = 400,
                    HttpStatus = "Bad Request",
                    Message = "Either PartnerTxnId or ReferenceId must be provided."
                };

                await SaveTransactionLogAsync(
                    reconcileRequest: request,
                    reconcileResponse: badRequestResponse,
                    requestTime: requestTime,
                    status: "Failed",
                    requestType: TranLogRequestType.Reconcile
                );

                return badRequestResponse;
            }

            // কোন আইডি দিয়ে সার্চ হবে তা নির্ধারণ
            bool isTollRequest = !string.IsNullOrWhiteSpace(request.PartnerTxnId);
            string searchTxnId = isTollRequest ? request.PartnerTxnId! : request.ReferenceId!;

            try
            {
                // ২. Primary Check: DoTransactions টেবিলে রেকর্ড খোঁজা
                // টোল হলে PartnerTxnId, এসবিএল চ্যানেল হলে RefNo1 (বা জেনেরিক আইডি কলাম) চেক করবে
                var transaction = await _context.DoTransactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => isTollRequest ? t.PartnerTxnId == searchTxnId : t.RefNo1 == searchTxnId);

                // Case 1: ডাটাবেজে সফলভাবে এক্সিকিউট ও সেভ হয়েছিল (Response Lost / Drop Case)
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
                            ReferenceId = transaction.RefNo1 ?? string.Empty,
                            WalletNo = transaction.PartnerId,
                            PartnerId = transaction.PartnerId,
                            BankTxnId = transaction.BankTxnId,
                            ReconStatus = transaction.TranStatus.Equals(TranStatus.Success, StringComparison.OrdinalIgnoreCase)
                                ? ReconStatus.Success
                                : ReconStatus.Failed,
                            TransactionAmount = transaction.TransactionAmount,
                            SourceAccountNo = transaction.SourceAccountNo,
                            TranMode = transaction.TranMode,
                            TransactionDate = transaction.BankTxnDate
                        }
                    };

                    await SaveTransactionLogAsync(
                        reconcileRequest: request,
                        reconcileResponse: successReconResponse,
                        requestTime: requestTime,
                        status: "Success",
                        requestType: TranLogRequestType.Reconcile,
                        tranMode: transaction.TranMode,
                        sblTxnId: transaction.BankTxnId
                    );

                    return successReconResponse;
                }

                // ৩. Secondary Check: TransactionLogs টেবিলে টেকনিক্যাল ফেলিউর রেকর্ড চেক
                var failedLog = await _context.TransactionLogs
                    .AsNoTracking()
                    .Where(l => isTollRequest ? l.PartnerTxnId == searchTxnId : l.PartnerTxnId == searchTxnId)
                    .OrderByDescending(l => l.RequestTimestamp)
                    .FirstOrDefaultAsync();

                // Case 2: রিকোয়েস্ট API-তে এসেছিল কিন্তু ভ্যালিডেশন/ডাটাবেজ এররে ফেল করেছিল
                if (failedLog != null)
                {
                    var failedReconResponse = new ReconcileTransactionResponse
                    {
                        HttpCode = 200,
                        HttpStatus = "OK",
                        Message = "Transaction attempted but failed during execution.",
                        Body = new ReconcileResultBody
                        {
                            PartnerTxnId = request.PartnerTxnId ?? failedLog.PartnerTxnId ?? string.Empty,
                            ReferenceId = request.ReferenceId ?? string.Empty,
                            WalletNo = failedLog.PartnerId ?? string.Empty,
                            PartnerId = failedLog.PartnerId ?? string.Empty,
                            BankTxnId = failedLog.SblTxnId ?? string.Empty,
                            ReconStatus = ReconStatus.Failed,
                            TransactionAmount = failedLog.TransactionAmount ?? 0,
                            SourceAccountNo = failedLog.AccountNo ?? string.Empty,
                            TranMode = failedLog.TranMode ?? string.Empty,
                            TransactionDate = failedLog.RequestTimestamp
                        }
                    };

                    await SaveTransactionLogAsync(
                        reconcileRequest: request,
                        reconcileResponse: failedReconResponse,
                        requestTime: requestTime,
                        status: "Success",
                        requestType: TranLogRequestType.Reconcile,
                        tranMode: failedLog.TranMode,
                        sblTxnId: failedLog.SblTxnId
                    );

                    return failedReconResponse;
                }

                // Case 3: রিকোয়েস্ট API পর্যন্ত পৌঁছায়নি (Not Found)
                var notFoundResponse = new ReconcileTransactionResponse
                {
                    HttpCode = 404,
                    HttpStatus = "Not Found",
                    Message = "No transaction record found with the provided identifier.",
                    Body = new ReconcileResultBody
                    {
                        PartnerTxnId = request.PartnerTxnId ?? string.Empty,
                        ReferenceId = request.ReferenceId ?? string.Empty,
                        WalletNo = string.Empty,
                        PartnerId = string.Empty,
                        BankTxnId = string.Empty,
                        ReconStatus = ReconStatus.Failed,
                        TransactionAmount = 0,
                        SourceAccountNo = string.Empty,
                        TranMode = string.Empty,
                        TransactionDate = null
                    }
                };

                await SaveTransactionLogAsync(
                    reconcileRequest: request,
                    reconcileResponse: notFoundResponse,
                    requestTime: requestTime,
                    status: "Failed",
                    requestType: TranLogRequestType.Reconcile
                );

                return notFoundResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Reconciliation for Identifier: {SearchTxnId}", searchTxnId);

                var errorResponse = new ReconcileTransactionResponse
                {
                    HttpCode = 500,
                    HttpStatus = "Internal Server Error",
                    Message = "An error occurred while handling the reconciliation request."
                };

                await SaveTransactionLogAsync(
                    reconcileRequest: request,
                    reconcileResponse: errorResponse,
                    requestTime: requestTime,
                    status: "Failed",
                    requestType: TranLogRequestType.Reconcile,
                    errorMessage: ex.Message
                );

                return errorResponse;
            }
        }

        private async Task SaveTransactionLogAsync(
            DoTransactionRequest request = null,
            DoTransactionResponse response = null,
            DateTime requestTime = default,
            string status = null,
            string requestType = null,
            string tranMode = null,
            string sblTxnId = null,
            decimal? balanceBefore = null,
            decimal? balanceAfter = null,
            string errorMessage = null,
            ReverseTransactionRequest reverseRequest = null,
            TopUpRequest topUpRequest = null,
            TopUpResponse topUpResponse = null,
            ReconcileTransactionRequest reconcileRequest = null,
            ReconcileTransactionResponse reconcileResponse = null)
        {
            try
            {
                // ১. রিকোয়েস্ট থেকে ফিল্ড এক্সট্র্যাক্ট করা (Tuple Type Explicitly Casted)
                string partnerId = null;
                string partnerTxnId = null;
                string accountNo = null;
                decimal? transactionAmount = null;
                object activeRequest = null;

                if (request != null)
                {
                    partnerId = request.PartnerId;
                    partnerTxnId = request.PartnerTxnId;
                    accountNo = request.SourceAccountNo;
                    transactionAmount = request.TransactionAmount;
                    activeRequest = request;
                }
                else if (reverseRequest != null)
                {
                    partnerId = reverseRequest.PartnerId;
                    partnerTxnId = reverseRequest.PartnerTxnId;
                    transactionAmount = reverseRequest.TransactionAmount;
                    activeRequest = reverseRequest;
                }
                else if (topUpRequest != null)
                {
                    partnerId = topUpRequest.WalletNo;
                    partnerTxnId = topUpRequest.ReferenceId;
                    accountNo = topUpRequest.SourceAccountNo;
                    transactionAmount = topUpRequest.TransactionAmount;
                    activeRequest = topUpRequest;
                }
                else if (reconcileRequest != null)
                {
                    partnerTxnId = reconcileRequest.PartnerTxnId ?? reconcileRequest.ReferenceId;
                    activeRequest = reconcileRequest;
                }

                // ২. রেসপন্স থেকে ফিল্ড এক্সট্র্যাক্ট করা
                string httpCode = null;
                string responseMsg = errorMessage;

                if (response != null)
                {
                    httpCode = response.HttpCode.ToString();
                    responseMsg ??= response.Message;
                }
                else if (topUpResponse != null)
                {
                    httpCode = topUpResponse.HttpCode.ToString();
                    responseMsg ??= topUpResponse.Message;
                }
                else if (reconcileResponse != null)
                {
                    httpCode = reconcileResponse.HttpCode.ToString();
                    responseMsg ??= reconcileResponse.Message;
                }

                object activeResponse = (object)response ?? (object)topUpResponse ?? (object)reconcileResponse;

                // ৩. লগার অবজেক্ট তৈরি
                var log = new TransactionLog
                {
                    Id = Guid.NewGuid(),
                    PartnerId = partnerId,
                    PartnerTxnId = partnerTxnId,
                    RequestType = requestType,
                    RequestData = activeRequest != null ? JsonSerializer.Serialize(activeRequest) : null,
                    ResponseData = activeResponse != null ? JsonSerializer.Serialize(activeResponse) : null,
                    ResponseCode = httpCode,
                    ResponseMessage = responseMsg,
                    RequestTimestamp = requestTime,
                    ResponseTimestamp = DateTime.UtcNow,
                    Status = status,
                    SblTxnId = sblTxnId,
                    AccountNo = accountNo,
                    TransactionAmount = transactionAmount,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    TranMode = tranMode
                };

                await _context.TransactionLogs.AddAsync(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                string txnId = request?.PartnerTxnId ?? reverseRequest?.PartnerTxnId ?? topUpRequest?.ReferenceId ?? reconcileRequest?.PartnerTxnId ?? reconcileRequest?.ReferenceId;
                _logger.LogError(ex, "Failed to write TransactionLog for TxnId: {TxnId}", txnId);
            }
        }
    }
}