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
        public DateTime SettlDate { get; set; }
        public DateTime BankTxnDate { get; set; }
        public string BatchProcessId { get; set; }
        public string CBSRef { get; set; }
        public decimal TotalAmount { get; set; }
        public string BankAccountNo { get; set; }
        public string Status { get; set; } 
        public string BrCode { get; set; } 
        public string UserId { get; set; } 
        public string SettlementOperation { get; set; } // // Toll/TopUp

        public DateTime? ProcessedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public string CBSResponse { get; set; }
    }
}