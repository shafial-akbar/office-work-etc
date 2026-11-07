using System;
using System.Collections.Generic;

namespace Etc.Shared.DTOs
{
    public class ReportRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Purpose { get; set; }       // Transaction, Reconciliation, Settlement, Reversal
        public string? TranMode { get; set; }      // Debit or Credit
        public string? SourceChannel { get; set; } // SPG, Counter, Wallet, Esheba
    }

    public class ReportResponse
    {
        public ReportSummaryDto Summary { get; set; } = new();
        public List<ReportDetailDto> Details { get; set; } = new();
    }

    public class ReportSummaryDto
    {
        public int TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FilteredTranMode { get; set; } = "ALL";
    }

    public class ReportDetailDto
    {
        public Guid Id { get; set; }
        public string BankTxnId { get; set; } = string.Empty;
        public DateTime BankTxnDate { get; set; }
        public string SourceAccountNo { get; set; } = string.Empty;
        public decimal TransactionAmount { get; set; }
        public string TranMode { get; set; } = string.Empty;
        public string SourceChannel { get; set; } = string.Empty;
        public string TranStatus { get; set; } = string.Empty;
        public string SettlStatus { get; set; } = string.Empty;

        // Toll (Debit) Fields
        public string? PartnerId { get; set; }     // Wallet No
        public string? PartnerTxnId { get; set; }  // Authority Txn Id

        // Common / Top-Up (Credit) Fields
        public string? RefNo1 { get; set; }        // Debit হলে Vehicle Reg No, Credit হলে Channel Ref No
        public DateTime? ChannelTransactionDate { get; set; }
    }

    public class MasterBalanceSummaryResponse
    {
        public decimal OpeningBalance { get; set; }     // FromDate-এর পূর্ববর্তী মাস্টার ব্যালেন্স
        public decimal TotalTopUpAmount { get; set; }   // নির্ধারিত মেয়াদের প্রকৃত টপ-আপ (রিফান্ড বাদে)
        public decimal GrossTollAmount { get; set; }    // নির্ধারিত মেয়াদের মোট টোল কালেকশন চেষ্টা (Success + Reversed)
        public decimal ReversedTollAmount { get; set; } // নির্ধারিত মেয়াদের রিভার্সড টোল
        public decimal NetTollAmount { get; set; }      // GrossToll - ReversedToll
        public decimal ClosingBalance { get; set; }     // OpeningBalance + TotalTopUp - NetToll

        public DateTime? FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }
}