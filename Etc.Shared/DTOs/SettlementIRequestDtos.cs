using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etc.Shared.DTOs
{
    public class SettlementRequest
    {
        public string BankTxnDate { get; set; }

        public string SettlementOperation { get; set; } // Toll/TopUp
        public string BrCode { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }

    public class SettlementResponse
    {
        public int HttpCode { get; set; }
        public string HttpStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

}
