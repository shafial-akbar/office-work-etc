using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etc.Shared.DTOs
{
    using System.ComponentModel.DataAnnotations;

    public class TopUpRequest
    {
        [Required(ErrorMessage = "Wallet No is required.")]
        public string WalletNo { get; set; }

        [Required(ErrorMessage = "Transaction amount is required.")]
        [Range(
                (double)Constants.TopUpLimits.MinTopUpAmount,
                (double)Constants.TopUpLimits.MaxTopUpAmount,
                ErrorMessage = "Transaction amount must be between {1} and {2} BDT.")]
        [RegularExpression(@"^\d+(\.\d{1,2})?$", ErrorMessage = "Transaction amount can have a maximum of 2 decimal places.")]
        public decimal TransactionAmount { get; set; }

        [Required(ErrorMessage = "ReferenceId is required.")]
        public string ReferenceId { get; set; }

        public string ChannelTransactionDate { get; set; }

        public string SourceAccountNo { get; set; }

        [Required(ErrorMessage = "SourceChannel is required.")]
        [RegularExpression("^[WOCE]$", ErrorMessage = "Invalid SourceChannel. Allowed values: W (Wallet), O (Online SPG), C (OTC Counter), E (E Sheba).")]
        public string SourceChannel { get; set; } = string.Empty;
    }

    public class TopUpResponse
    {
        public int HttpCode { get; set; }
        public string HttpStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public TopUpResultBody? Body { get; set; }
    }

    public class TopUpResultBody
    {
        public string WalletNo { get; set; }
        public decimal TransactionAmount { get; set; }
        public decimal NewBalance { get; set; }
        public string BankTxnId { get; set; }
        public string ReferenceId { get; set; }
        public string TranStatus { get; set; }
        public string MobileNo { get; set; }
        
    }


}
