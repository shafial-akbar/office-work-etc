namespace Etc.Shared.DTOs
{
    public class PostTransactionRequest
    {
        public PostTransactionRequest()
        {
            Debits = new List<Debit>();
            Credits = new List<Credit>();
        }
        public string UserName { get; set; }
        public string ServiceCode { get; set; }
        public string SpCode { get; set; }
        public string ChannelId { get; set; }
        public string ReferenceNo { get; set; }
        public string ReferenceDate { get; set; }
        public string OrigBrnCode { get; set; }
        public string BatchNarration { get; set; }
        public List<Debit> Debits { get; set; }
        public List<Credit> Credits { get; set; }
    }

    public class Debit
    {
        public string AccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string Narration { get; set; }
        public string CreditNarration { get; set; }
        public string GlAccCode { get; set; }
        public string GlBrnCode { get; set; }
    }

    public class Credit
    {
        public string AccountNumber { get; set; }
        public decimal Amount { get; set; }
        public string Narration { get; set; }
        public string DebitNarration { get; set; }
        public string GlAccCode { get; set; }
        public string GlBrnCode { get; set; }
    }


    public class PostTransactionResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public string BankTransactionRef { get; set; }
        public string OtherTransacRef { get; set; }
        public string DebitAccountNo { get; set; }
        public string CreditAccountNo { get; set; }
        public decimal TranAmount { get; set; }
        public string DebitAcMobile { get; set; }
        public string DebitAccountName { get; set; }
        public string CBSTxDate_YYYY_MM_DD { get; set; }
        public string CBSTxBrnCode { get; set; }
        public string CBSVoucherNo { get; set; }
        public string CBSStatusCode { get; set; }
        public string CBSStatusDescription { get; set; }
    }
}
