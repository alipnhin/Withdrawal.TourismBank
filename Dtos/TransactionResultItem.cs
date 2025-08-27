namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;


internal class TransactionResultItem
{
    public long? LineNumber { get; set; }
    public string Amount { get; set; }
    public string FinalState { get; set; }
    public string FinalMessage { get; set; }
    public string Status { get; set; }
    public string TransactionType { get; set; }
    public string TransactionDate { get; set; }
    public string TransactionDescription { get; set; }
    public string RefrenceNumber { get; set; }
    public string BranchCode { get; set; }
    public string DocumentNumber { get; set; }
    public string DestinationBankCode { get; set; }
    public string DestinationBankName { get; set; }
    public string TransactionCommission { get; set; }
    public string ErrorDescription { get; set; }
}

