using TPG.SI.TadbirPay.Infrastructure.Dtos.Payment;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Response;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Payment;

/// <summary>
/// Extension methods برای PaymentOrderDto جهت مدیریت metadata - نسخه بازطراحی شده
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
    /// آیا نیاز به Execute دارد - لاجیک جدید بر اساس وضعیت بانک
    /// </summary>
    public static bool RequiresExecution(this PaymentOrderDto paymentOrder)
    {
        var metadata = paymentOrder.GetPaymentMetadata();
        return metadata.ShouldExecuteDoPayment();
    }

    /// <summary>
    /// آیا باید از سرویس InquiryFromCore استفاده کند - لاجیک جدید
    /// </summary>
    public static bool ShouldUseInquiryFromCore(this PaymentOrderDto paymentOrder)
    {
        var metadata = paymentOrder.GetPaymentMetadata();
        return metadata.ShouldUseDetailedInquiry();
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
    /// به‌روزرسانی وضعیت استعلام بر اساس پاسخ بانک
    /// </summary>
    public static void UpdateInquiryStatus(this PaymentOrderDto paymentOrder, string bankStatus)
    {
        paymentOrder.UpdatePaymentMetadata(meta => meta.UpdateFromBankStatus(bankStatus));
    }

    /// <summary>
    /// به‌روزرسانی وضعیت‌های تراکنش‌ها بر اساس نتیجه استعلام تفصیلی
    /// </summary>
    internal static void UpdateTransactionStatuses(this PaymentOrderDto paymentOrder, TransactionInquiryResult result)
    {
        var metadata = paymentOrder.GetPaymentMetadata();

        foreach (var transaction in result.Transactions)
        {
            metadata.UpdateTransactionStatus(transaction.LineNumber, transaction.Status);
        }

        paymentOrder.UpdatePaymentMetadata(metadata);
    }

    /// <summary>
    /// علامت‌گذاری تکمیل یک مرحله خاص
    /// </summary>
    public static void MarkPhaseCompleted(this PaymentOrderDto paymentOrder, PaymentPhase phase)
    {
        paymentOrder.UpdatePaymentMetadata(meta =>
        {
            meta.CurrentPhase = phase;
            meta.LastInquiryTime = DateTime.UtcNow;
        });
    }

    /// <summary>
    /// آیا در حالت نهایی است
    /// </summary>
    public static bool IsInFinalState(this PaymentOrderDto paymentOrder)
    {
        var metadata = paymentOrder.GetPaymentMetadata();
        return metadata.IsInFinalState();
    }

    /// <summary>
    /// دریافت توضیح وضعیت فعلی
    /// </summary>
    public static string GetCurrentStatusDescription(this PaymentOrderDto paymentOrder)
    {
        var metadata = paymentOrder.GetPaymentMetadata();
        return metadata.GetCurrentStatusDescription();
    }

    /// <summary>
    /// آیا DoPayment تکمیل شده است
    /// </summary>
    public static bool IsDoPaymentCompleted(this PaymentOrderDto paymentOrder)
    {
        var metadata = paymentOrder.GetPaymentMetadata();
        return metadata.IsDoPaymentCompleted;
    }

    /// <summary>
    /// دریافت مرحله فعلی پرداخت
    /// </summary>
    public static PaymentPhase GetCurrentPhase(this PaymentOrderDto paymentOrder)
    {
        var metadata = paymentOrder.GetPaymentMetadata();
        return metadata.CurrentPhase;
    }

    /// <summary>
    /// آیا نیاز به retry برای Execute دارد
    /// </summary>
    public static bool CanRetryExecution(this PaymentOrderDto paymentOrder)
    {
        var metadata = paymentOrder.GetPaymentMetadata();
        return metadata.LastBankStatus?.ToUpper() == "READY" &&
               metadata.ExecutionAttempts < 3 &&
               !metadata.IsDoPaymentCompleted;
    }

    /// <summary>
    /// زمان آخرین استعلام
    /// </summary>
    public static DateTime? GetLastInquiryTime(this PaymentOrderDto paymentOrder)
    {
        var metadata = paymentOrder.GetPaymentMetadata();
        return metadata.LastInquiryTime;
    }

    /// <summary>
    /// آیا نیاز به استعلام مجدد دارد (بر اساس زمان)
    /// </summary>
    public static bool NeedsInquiryRefresh(this PaymentOrderDto paymentOrder, TimeSpan maxAge)
    {
        var lastInquiry = paymentOrder.GetLastInquiryTime();
        return !lastInquiry.HasValue || DateTime.UtcNow - lastInquiry.Value > maxAge;
    }
}