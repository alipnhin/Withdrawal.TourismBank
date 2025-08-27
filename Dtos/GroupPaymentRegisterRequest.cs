namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;


/// <summary>
/// مدل درخواست ثبت پرداخت گروهی
/// </summary>
internal class GroupPaymentRegisterRequest
{
    public string TransactionId { get; set; }
    public bool AutoContinue { get; set; } = true;
    public string CustomerNumber { get; set; }
    public string SourceDeposit { get; set; }
    public string RefundDeposit { get; set; }
    public string SourceDepositCommission { get; set; }
    public string SourceDescription { get; set; }
    public string BillNumber { get; set; }

    public long? ThresholdAmount { get; set; }
    public List<DocumentItem> DocumentItems { get; set; }
}
