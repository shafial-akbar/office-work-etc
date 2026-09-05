using Etc.Shared.Models;

namespace Etc.Shared.Constants
{
    public class BaseStatus
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
        public const string Blocked = "Blocked";
    }

    public class CustomerStatus : BaseStatus
    {
        // Private constructor ensure করে কেউ new CustomerStatus() করতে পারবে না
        private CustomerStatus() { }
    }

    public class VehicleStatus : BaseStatus
    {
        private VehicleStatus() { }
    }

    public class WalletStatus : BaseStatus
    {
        private WalletStatus() { }
    }

    public static class WalletType
    {
        public const string Bank = "BANK";
    }

    public static class TranStatus
    {
        public const string New = "New";
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Success = "Success";
        public const string Failed = "Failed";
        public const string Reversed = "Reversed";
        public const string Refunded = "Refunded";
        public const string Disputed = "Disputed";
        public const string Settled = "Settled";
    }

    public static class ReconStatus
    {
        public const string Success = "Success";
        public const string Failed = "Failed";
    }

    public static class SettlementStatus
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Settled = "Settled";
        public const string Reversed = "Reversed";
    }

    public static class SettlementOperation
    {
        public const string Toll = "Toll";
        public const string TopUp = "TopUp";
    }

    public static class TranMode
    {
        public const string Debit = "Debit";
        public const string Credit = "Credit";
    }

    public static class TranLogRequestType
    {
        public const string TopUp = "TopUp";
        public const string TollDeduction = "TollDeduction";
        public const string TollReverse = "TollReverse";
        public const string Reconcile = "Reconcile";
        public const string AccountInquiry = "AccountInquiry";
    }

    public static class SourceChannel
    {
        public const string SPG = "SPG";
        public const string Counter = "Counter";
        public const string Wallet = "Wallet";
        public const string Esheba = "Esheba";
    }

    public static class ReportType
    {
        public const string Transaction = "Transaction";       // ১. Active Success (রিভার্সড বাদ দিয়ে)
        public const string Reconciliation = "Reconciliation"; // For Top-up channels & Toll Authority (Status: Success & Reversed)
        public const string Settlement = "Settlement"; // For EOD Disbursement/Settlement Batch (Status: Settled)
        public const string Reversal = "Reversal";             // ৪. Only Reversed Transactions (অডিট ও রিফান্ড ট্রেসিং)
    }

    public static class CompanyConfig
    {
        public const int DefaultCompanyOid = 6;
    }

    public static class TopUpLimits
    {
        public const decimal MinTopUpAmount = 50.00m;
        public const decimal MaxTopUpAmount = 10000.00m;
        public const int MaxDailyTopUpCount = 20;
    }
}
