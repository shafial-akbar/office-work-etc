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
        public DateTime SettlementDate { get; set; }
        public string ProcessId { get; set; }
        public decimal TotalAmount { get; set; }
        public string BankAccountNo { get; set; }
        public string Status { get; set; } // PENDING, PROCESSING, COMPLETED, FAILED
        public DateTime? ProcessedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}