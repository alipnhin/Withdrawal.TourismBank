namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;

internal class RegisterGroupPaymentResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
    public string TrackingId { get; set; }

    public string ErrorCode { get; set; }
}
