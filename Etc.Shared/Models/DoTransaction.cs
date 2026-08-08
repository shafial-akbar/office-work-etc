using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Etc.Shared.Models
{
    public class DoTransaction
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; }

        public Guid WalletId { get; set; }

        [Required]
        public string PartnerId { get; set; } = string.Empty;

        [Required]
        public string PartnerTxnId { get; set; } = string.Empty;

        [Required]
        public DateTime PartnerTransactionDate { get; set; }

        [Required]
        public string SourceAccountNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Transaction amount is required.")]
        [Column(TypeName = "numeric(18,2)")]
        [RegularExpression(@"^\d+(\.\d{1,2})?$", ErrorMessage = "Transaction amount can have a maximum of 2 decimal places.")]
        public decimal TransactionAmount { get; set; }

        [Required]
        public string ResponseCode { get; set; } = string.Empty;

        [Required]
        public string ResponseMessage { get; set; } = string.Empty;

        // Fixed: Ref fields cleaned up
        public string? RefNo1 { get; set; }
        public string? RefNo2 { get; set; }
        public string? RefNo3 { get; set; }
        public string? RefNo4 { get; set; }
        public string? RefNo5 { get; set; }

        [Required]
        public DateTime ChannelTransactionDate { get; set; }

        [Required]
        public string BankTxnId { get; set; } = string.Empty;

        [Required]
        public DateTime BankTxnDate { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "Transaction Status is required.")]
        [RegularExpression("^(New|Pending|Processing|Success|Failed|Reversed|Refunded|Disputed|Settled)$", ErrorMessage = "Invalid TranStatus. Allowed values are: New, Pending, Processing, Success, Failed, Reversed, Refunded, Disputed, Settled")]
        public string TranStatus { get; set; } = string.Empty;

        [RegularExpression("^(Pending|Processing|Settled)$", ErrorMessage = "Invalid SettlStatus. Allowed values are: Pending, Processing, Settled")]
        public string? SettlStatus { get; set; }

        public DateTime? SettlDate { get; set; }

        public string? BatchProcessId { get; set; }

        [Required]
        [RegularExpression("^(Debit|Credit)$", ErrorMessage = "Invalid TranMode. Allowed values are: Debit, Credit")]
        public string TranMode { get; set; } = string.Empty;

        [Required(ErrorMessage = "SourceChannel is required.")]
        [RegularExpression("^(SPG|Counter|Wallet|Esheba)$", ErrorMessage = "Invalid SourceChannel. Allowed values are: SPG, Counter, Wallet, Esheba")]
        public string SourceChannel { get; set; } = string.Empty;

        // Fixed: Nullability
        public Wallet Wallet { get; set; } = null!;
    }    
}

