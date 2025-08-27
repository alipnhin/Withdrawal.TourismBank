using System.Text.Json.Serialization;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;

internal class InquiryRequest
{
    public string TransactionId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FirstIndex { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LastIndex { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LineNumber { get; set; }


}
