using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etc.Shared.DTOs
{
    public class DoTransactionRequest
    {
        [Required(ErrorMessage = "PartnerId is required.")]
        public string PartnerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "PartnerTxnId is required.")]
        public string PartnerTxnId { get; set; } = string.Empty;

        [Required(ErrorMessage = "PartnerTransactionDate is required.")]
        public string PartnerTransactionDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "SourceAccountNo is required.")]
        public string SourceAccountNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Transaction amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Transaction amount must be greater than 0.")]
        [RegularExpression(@"^\d+(\.\d{1,2})?$", ErrorMessage = "Transaction amount can have a maximum of 2 decimal places.")]
        public decimal TransactionAmount { get; set; }

        public string? RefNo1 { get; set; } = string.Empty;
        public string? RefNo2 { get; set; } = string.Empty;
        public string? RefNo3 { get; set; } = string.Empty;
        public string? RefNo4 { get; set; } = string.Empty;
        public string? RefNo5 { get; set; } = string.Empty;
    }

    public class DoTransactionResponse
    {
        public int HttpCode { get; set; }
        public string HttpStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public TransactionResultBody? Body { get; set; }
    }

    public class TransactionResultBody
    {
        public string BankTxnId { get; set; } = string.Empty;
        public string PartnerTxnId { get; set; } = string.Empty;
        public string TranStatus { get; set; } = string.Empty;
        public decimal TransactionAmount { get; set; }
    }

    public class ReverseTransactionRequest
    {
        [Required(ErrorMessage = "PartnerId (Wallet No) is required.")]
        public string PartnerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "PartnerTxnId is required.")]
        public string PartnerTxnId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Transaction amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Transaction amount must be greater than 0.")]
        [RegularExpression(@"^\d+(\.\d{1,2})?$", ErrorMessage = "Transaction amount can have a maximum of 2 decimal places.")]
        public decimal TransactionAmount { get; set; }

    }

    // Stored procedure Dtos
    public class SpDeductTollResult
    {
        public int http_code { get; set; }
        public string status_message { get; set; } = string.Empty;
        public string bank_txn_id { get; set; } = string.Empty;
        public decimal balance_before { get; set; }
        public decimal balance_after { get; set; }
    }

}
