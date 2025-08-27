using System.Text.Json;
using System.Text.Json.Serialization;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;





internal class InquiryResultData
{
    public string TransactionStatus { get; set; }
    public object RecordErrorsList { get; set; }
    public InquiryResultObject Result { get; set; }
}

internal class InquiryResultObject
{
    public string TransactionId { get; set; }
    public string DateTime { get; set; }
    public string CustomerNumber { get; set; }
    public string State { get; set; }
    public string LineCount { get; set; }
    public string TotalAmount { get; set; }
    public string TotalInternalAmount { get; set; }
    public string TotalPayaAmount { get; set; }
    public string TotalSatnaAmount { get; set; }
    public string TotalCommissionPayaAmount { get; set; }
    public string TotalCommissionSatnaAmount { get; set; }
    public string TotalCommissionInternalAmount { get; set; }
    public string CountPaya { get; set; }
    public string InternalRecordCount { get; set; }
    public string SourceDeposit { get; set; }
    public string RefundDeposit { get; set; }
    public string SourceDepositCommission { get; set; }
    public object ThresholdAmount { get; set; }
}
