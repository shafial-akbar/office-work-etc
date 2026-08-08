using System;
using System.Threading.Tasks;
using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface IWalletTransactionService
    {
        // ১. ওয়ালেট টপ-আপ (Wallet Credit)
        Task<TopUpResponse> TopUpWalletAsync(TopUpRequest topUpDto);

        // ২. টোল কালেকশন ও ব্যালেন্স কাটা (Toll Amount Debit/Deduction)
        Task<DoTransactionResponse> DeductTollAsync(DoTransactionRequest deductionDto);

        // ৩. টোল ট্রানজেকশন রিভার্সাল বা রিফান্ড (Toll Amount Reversal/Credit)
        Task<DoTransactionResponse> ReverseTollAsync(ReverseTransactionRequest reversalDto);

        // 4. Reconcile
        Task<ReconcileTransactionResponse> ReconcileTransactionAsync(ReconcileTransactionRequest ReconcileDto);
    }
}