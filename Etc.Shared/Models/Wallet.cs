using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Etc.Shared.Models
{
    public class Wallet
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }

        [Required]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "WalletNo must be exactly 13 digits.")]
        public string WalletNo { get; set; } = string.Empty;

        public string MobileNo { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Currency { get; set; } = "BDT";
        public string Status { get; set; } = "Active";
        public string CompanyName { get; set; } = "SONALI BANK PLC";
        public string Type { get; set; } = "BANK";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public Customer Customer { get; set; } = null!;

        [JsonIgnore]
        public ICollection<DoTransaction> DoTransactions { get; set; } = new List<DoTransaction>();

        [JsonIgnore]
        public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    }
}