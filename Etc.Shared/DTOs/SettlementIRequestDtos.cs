using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etc.Shared.DTOs
{
    // পার্টনারের পাঠানো প্রতিটি ট্রানজেকশনের আইটেম
    public class SettlementItemDto
    {
        [Required]
        public string PartnerTxnId { get; set; } = string.Empty;

        [Required]
        public decimal TransactionAmount { get; set; }
    }

    // সেটেলমেন্টের প্রধান রিকোয়েস্ট অবজেক্ট
    public class BatchSettlementRequest
    {
        [Required(ErrorMessage = "PartnerId is required.")]
        public string PartnerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Settlement batch date/identifier is required.")]
        public string SettlementBatchId { get; set; } = string.Empty;

        [Required(ErrorMessage = "TotalSettlementAmount is required.")]
        public decimal TotalSettlementAmount { get; set; }

        [Required(ErrorMessage = "Transaction list cannot be empty.")]
        public List<SettlementItemDto> Transactions { get; set; } = new();
    }

    // সেটেলমেন্টের রেসপন্স অবজেক্ট
    public class BatchSettlementResponse
    {
        public int HttpCode { get; set; }
        public string HttpStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public SettlementResultBody? Body { get; set; }
    }

    public class SettlementResultBody
    {
        public string SettlementBatchId { get; set; } = string.Empty;
        public string PartnerId { get; set; } = string.Empty;
        public int TotalSubmittedCount { get; set; }
        public int TotalSettledCount { get; set; }
        public decimal TotalSettledAmount { get; set; }
        public string SettlementStatus { get; set; } = string.Empty; // "Settled" or "Failed"
        public List<string> MismatchedPartnerTxnIds { get; set; } = new();
    }
}
