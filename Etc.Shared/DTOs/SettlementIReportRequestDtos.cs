using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etc.Shared.DTOs
{
    public class SettlementReportRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? TranMode { get; set; } // "Debit" or "Credit" (Optional filter)
        public string? SourceChannel { get; set; } // "SPG", "Counter", "Wallet", "Esheba" (Optional - Only for Credit Mode)
    }

    public class SettlementReportResponse
    {
        public SettlementSummaryDto Summary { get; set; } = new();
        public List<SettlementDetailDto> Details { get; set; } = new();
    }

    public class SettlementSummaryDto
    {
        public int TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FilteredTranMode { get; set; } = "ALL";
    }

    public class SettlementDetailDto
    {
        public Guid Id { get; set; }
        public string BankTxnId { get; set; } = string.Empty;
        public DateTime BankTxnDate { get; set; }
        public string SourceAccountNo { get; set; } = string.Empty;
        public decimal TransactionAmount { get; set; }
        public string TranMode { get; set; } = string.Empty;
        public string SourceChannel { get; set; } = string.Empty;
        public string SettlStatus { get; set; } = string.Empty;

        // Credit Mode Fields
        public string? PartnerId { get; set; }
        public string? PartnerTxnId { get; set; }

        // Debit Mode Fields
        public string? RefNo1 { get; set; }
        public DateTime? ChannelTransactionDate { get; set; }
    }
}
