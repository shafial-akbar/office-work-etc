using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Etc.Shared.Models
{
    public class Settlement
    {
        [Key]
        [Column("Id")]

        public Guid Id { get; set; }
        public DateTime BankTxnDate { get; set; }
        public string BatchProcessId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal ReverseAmount { get; set; }
        public decimal NetSettlementAmount { get; set; }
        public int TotalCount { get; set; }
        public int ReverseCount { get; set; }
        public int NetCount { get; set; }
        public string SettlementAccountNo { get; set; } = string.Empty;
        public string ParkingGL { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SettlementOperation { get; set; } = string.Empty; // // Toll/TopUp
        public string ProcessBrCode { get; set; } = string.Empty;
        public string SettleBrCode { get; set; } = string.Empty;
        public string ProcessedBy { get; set; } = string.Empty;
        public string SettledBy { get; set; } = string.Empty;
        public DateTime? ProcessedAt { get; set; }
        public DateTime? SettledAt { get; set; }
        public string CBSResponse { get; set; } = string.Empty;
    }
}