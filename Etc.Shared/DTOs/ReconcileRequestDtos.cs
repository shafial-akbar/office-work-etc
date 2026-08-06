using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etc.Shared.DTOs
{
    public class ReconcileTransactionRequest
    {
        [Required(ErrorMessage = "PartnerTxnId is required.")]
        public string PartnerTxnId { get; set; } = string.Empty;

        public string? PartnerId { get; set; }
    }

    public class ReconcileTransactionResponse
    {
        public int HttpCode { get; set; }
        public string HttpStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public ReconcileResultBody? Body { get; set; }
    }

    public class ReconcileResultBody
    {
        public string PartnerTxnId { get; set; } = string.Empty;
        public string BankTxnId { get; set; } = string.Empty;
        public string ReconStatus { get; set; } = string.Empty; // SUCCESS, FAILED, NOT_FOUND, PENDING
        public decimal TransactionAmount { get; set; }
        public string PartnerId { get; set; } = string.Empty;
        public string SourceAccountNo { get; set; } = string.Empty;
        public string TranMode { get; set; } = string.Empty;
        public DateTime? TransactionDate { get; set; }
    }
}
