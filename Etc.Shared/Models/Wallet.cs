using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Etc.Shared.Models
{
    public class Wallet
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }

        [Required]
        [StringLength(14, MinimumLength = 14, ErrorMessage = "WalletNo must be exactly 14 digits.")]
        [RegularExpression(@"^99\d{12}$", ErrorMessage = "WalletNo must start with '99' followed by 12 digits.")]
        public string WalletNo { get; set; } = string.Empty;

        public string MobileNo { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Currency { get; set; } = "BDT";
        public string Status { get; set; } = "Active";
        public string CompanyName { get; set; } = "SONALI BANK PLC";
        public string Type { get; set; } = "BANK";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Fixed: Nullability & Collections Initialized
        public Customer Customer { get; set; } = null!;
        public ICollection<DoTransaction> DoTransactions { get; set; } = new List<DoTransaction>();
        public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    }
}