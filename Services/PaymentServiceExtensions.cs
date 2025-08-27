using TPG.SI.TadbirPay.Infrastructure.Dtos.Gateway;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Services;

/// <summary>
/// Extension methods برای PaymentService جهت انتخاب سرویس مناسب
/// </summary>
internal static class PaymentServiceExtensions
{
    /// <summary>
    /// استعلام از سرویس مناسب بر اساس وضعیت پرداخت
    /// </summary>
    public static async Task<InquiryResult> InquiryGroupPaymentAsync(
        this IPaymentService paymentService,
        GatewayInfoDto gatewayInfo,
        InquiryRequest request,
        bool useInquiryFromCore = false)
    {
        // این متد را می‌توان برای انتخاب سرویس مناسب استفاده کرد
        // در حال حاضر همان متد اصلی را صدا می‌زند
        return await paymentService.InquiryGroupPaymentAsync(gatewayInfo, request);
    }

    /// <summary>
    /// بررسی آمادگی برای اجرای DoPayment
    /// </summary>
    public static async Task<(bool IsReady, string Status, string Message)> CheckExecutionReadinessAsync(
        this IPaymentService paymentService,
        string transactionId,
        GatewayInfoDto gatewayInfo)
    {
        try
        {
            var request = new InquiryRequest { TransactionId = transactionId };
            var result = await paymentService.InquiryGroupPaymentAsync(gatewayInfo, request);

            if (!result.IsSuccess)
            {
                return (false, "", result.Message);
            }

            var isReady = result.Status?.ToUpper() == "READY";
            return (isReady, result.Status ?? "", result.Message ?? "");
        }
        catch (Exception ex)
        {
            return (false, "", $"خطا در بررسی آمادگی: {ex.Message}");
        }
    }
}