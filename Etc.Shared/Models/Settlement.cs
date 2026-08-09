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
        public string BatchProcessId { get; set; }
        public string CBSRef { get; set; }
        public decimal TotalAmount { get; set; }
        public string BankAccountNo { get; set; }
        public string Status { get; set; } 
        public string SettlementOperation { get; set; } // // Toll/TopUp
        public string ProcessBrCode { get; set; }
        public string SettleBrCode { get; set; }
        public string ProcessedBy { get; set; }
        public string SettledBy { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime? SettledAt { get; set; }
        public string CBSResponse { get; set; }
    }
}