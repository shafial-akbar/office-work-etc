using System;
using System.Threading.Tasks;
using Etc.Shared.DTOs;
using Etc.Shared.Models;

namespace Etc.Shared.Interfaces
{
    public interface IWalletTransactionService
    {
        // ১. ওয়ালেট টপ-আপ (Wallet Credit)
        Task<DoTransactionResponse> TopUpWalletAsync(DoTransactionRequest topUpDto);

        // ২. টোল কালেকশন ও ব্যালেন্স কাটা (Toll Amount Debit/Deduction)
        Task<DoTransactionResponse> DeductTollAsync(DoTransactionRequest deductionDto);

        // ৩. টোল ট্রানজেকশন রিভার্সাল বা রিফান্ড (Toll Amount Reversal/Credit)
        Task<DoTransactionResponse> ReverseTollAsync(DoTransactionRequest reversalDto);

        // ৪. ওয়ালেটের বর্তমান ব্যালেন্স চেক
        Task<decimal> GetBalanceAsync(string walletNo);
    }
}