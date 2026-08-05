namespace Etc.Shared.Models
{
    public class TransactionLog
    {
        public Guid Id { get; set; }
        public string PartnerId { get; set; }
        public string PartnerTxnId { get; set; }
        public string RequestType { get; set; } // "AccountInquiry", "DoTransaction", "DoReverse"
        public string RequestData { get; set; }
        public string ResponseData { get; set; }
        public string ResponseCode { get; set; }
        public string ResponseMessage { get; set; }
        public DateTime RequestTimestamp { get; set; }
        public DateTime? ResponseTimestamp { get; set; }
        public string Status { get; set; } // "Success", "Failed"
        public string SblTxnId { get; set; }
        public string AccountNo { get; set; }
        public decimal? TransactionAmount { get; set; }
    }
}
