using TPG.SI.TadbirPay.Infrastructure.Enums;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;


internal class TransactionResult
{
    public long LineNumber { get; set; }
    public string Amount { get; set; }
    public string Status { get; set; }
    public string TransactionType { get; set; }
    public string FinalState { get; set; }
    public string FinalMessage { get; set; }
    public PaymentMethodEnum PaymentMethod { get; set; }
    public string ErrorDescription { get; set; }
    public string RefrenceNumber { get; set; }
    public string BranchCode { get; set; }
    public string DocumentNumber { get; set; }
    public string DestinationBankCode { get; set; }
    public string DestinationBankName { get; set; }
}
