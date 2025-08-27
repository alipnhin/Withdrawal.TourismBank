using TPG.SI.TadbirPay.Infrastructure.Dtos.Payment;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Payment;

/// <summary>
/// Extension methods برای PaymentOrderDto جهت مدیریت metadata
/// </summary>
public static class PaymentOrderDtoExtensions
{
    /// <summary>
    /// دریافت metadata پرداخت
    /// </summary>
    public static PaymentOrderMetadata GetPaymentMetadata(this PaymentOrderDto paymentOrder)
    {
        return PaymentOrderMetadata.FromJson(paymentOrder.MetaData);
    }

    /// <summary>
    /// به‌روزرسانی metadata پرداخت
    /// </summary>
    public static void UpdatePaymentMetadata(this PaymentOrderDto paymentOrder, PaymentOrderMetadata metadata)
    {
        paymentOrder.MetaData = metadata.ToJson();
    }

    /// <summary>
    /// به‌روزرسانی metadata با action
    /// </summary>
    public static void UpdatePaymentMetadata(this PaymentOrderDto paymentOrder, Action<PaymentOrderMetadata> updateAction)
    {
        var metadata = paymentOrder.GetPaymentMetadata();
        updateAction(metadata);
        paymentOrder.UpdatePaymentMetadata(metadata);
    }

    /// <summary>
    /// آیا نیاز به Execute دارد
    /// </summary>
    public static bool NeedsExecution(this PaymentOrderDto paymentOrder)
    {
        return paymentOrder.GetPaymentMetadata().NeedsExecution();
    }

    /// <summary>
    /// آیا باید از سرویس InquiryFromCore استفاده کند
    /// </summary>
    public static bool ShouldUseInquiryFromCore(this PaymentOrderDto paymentOrder)
    {
        return paymentOrder.GetPaymentMetadata().ShouldUseInquiryFromCore();
    }

    /// <summary>
    /// علامت‌گذاری شروع مرحله Execute
    /// </summary>
    public static void MarkExecutionStarted(this PaymentOrderDto paymentOrder)
    {
        paymentOrder.UpdatePaymentMetadata(meta => meta.MarkExecutionStarted());
    }

    /// <summary>
    /// علامت‌گذاری تکمیل موفق مرحله Execute
    /// </summary>
    public static void MarkExecutionCompleted(this PaymentOrderDto paymentOrder)
    {
        paymentOrder.UpdatePaymentMetadata(meta => meta.MarkExecutionCompleted());
    }

    /// <summary>
    /// علامت‌گذاری خطا در مرحله Execute
    /// </summary>
    public static void MarkExecutionFailed(this PaymentOrderDto paymentOrder, string error)
    {
        paymentOrder.UpdatePaymentMetadata(meta => meta.MarkExecutionFailed(error));
    }

    /// <summary>
    /// به‌روزرسانی وضعیت استعلام
    /// </summary>
    public static void UpdateInquiryStatus(this PaymentOrderDto paymentOrder, string bankStatus)
    {
        paymentOrder.UpdatePaymentMetadata(meta => meta.UpdateInquiryStatus(bankStatus));
    }
}