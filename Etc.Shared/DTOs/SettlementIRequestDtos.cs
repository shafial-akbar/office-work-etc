using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etc.Shared.DTOs
{
    public class DataprocessRequest
    {
        [Required(ErrorMessage = "BankTxnDate is required.")]
        public string BankTxnDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "SettlementOperation is required.")]
        public string SettlementOperation { get; set; } = string.Empty; // Toll/TopUp

        [Required(ErrorMessage = "BrCode is required.")]
        public string BrCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "UserId is required.")]
        public string UserId { get; set; } = string.Empty;
    }

    public class DataprocessResponse
    {
        public int HttpCode { get; set; }
        public string HttpStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class SettlementRequest
    {
        [Required(ErrorMessage = "BankTxnDate is required.")]
        public string BankTxnDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "SettlementOperation is required.")]
        public string SettlementOperation { get; set; } = string.Empty; // Toll/TopUp

        [Required(ErrorMessage = "BrCode is required.")]
        public string BrCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "UserId is required.")]
        public string UserId { get; set; } = string.Empty;
    }

    public class SettlementResponse
    {
        public int HttpCode { get; set; }
        public string HttpStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

}
