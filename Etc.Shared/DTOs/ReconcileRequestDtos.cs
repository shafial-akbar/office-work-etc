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
        // টোল অথরিটি শুধু এটি পাঠাবে: { "partnerTxnId": "TXN_12345" }
        public string? PartnerTxnId { get; set; }

        // এসবিএল চ্যানেলগুলো শুধু এটি পাঠাবে: { "referenceId": "REF_67890" }
        public string? ReferenceId { get; set; }
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
        public string ReferenceId { get; set; } = string.Empty;
        public string WalletNo { get; set; } = string.Empty;
        public string PartnerId { get; set; } = string.Empty;
        public string BankTxnId { get; set; } = string.Empty;
        public string ReconStatus { get; set; } = string.Empty;
        public decimal TransactionAmount { get; set; }
        public string SourceAccountNo { get; set; } = string.Empty;
        public string TranMode { get; set; } = string.Empty;
        public DateTime? TransactionDate { get; set; }
    }
}
