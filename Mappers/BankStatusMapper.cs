using TPG.SI.TadbirPay.Infrastructure.Enums;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Mappers;

/// <summary>
/// تبدیل وضعیت‌های بانک گردشگری به enum های استاندارد سیستم
/// </summary>
internal static class BankStatusMapper
{
    /// <summary>
    /// نقشه تبدیل وضعیت سفارش پرداخت
    /// </summary>
    private static readonly Dictionary<string, PaymentStatusEnum> OrderStatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // وضعیت‌های در حال پردازش
        ["GROUP_PAYMENT_WAITING_STATE"] = PaymentStatusEnum.SubmittedToBank,
        ["GROUP_PAYMENT_REGISTERED_STATE"] = PaymentStatusEnum.SubmittedToBank,
        ["GROUP_PAYMENT_UPLOADING_STATE"] = PaymentStatusEnum.SubmittedToBank,
        ["PROCESSING"] = PaymentStatusEnum.SubmittedToBank,
        ["READY"] = PaymentStatusEnum.SubmittedToBank,

        // وضعیت‌های نهایی موفق
        ["DONE"] = PaymentStatusEnum.BankSucceeded, // نیاز به بررسی تفصیلی

        // وضعیت‌های خطا
        ["GROUP_PAYMENT_ERROR_STATE"] = PaymentStatusEnum.BankRejected,
        ["FAILED"] = PaymentStatusEnum.BankRejected,
        ["FAIL"] = PaymentStatusEnum.BankRejected,

        // وضعیت‌های لغو
        ["CANCELED"] = PaymentStatusEnum.Canceled,
        ["CANCELLED"] = PaymentStatusEnum.Canceled,

        // وضعیت‌های منقضی
        ["EXPIRED"] = PaymentStatusEnum.Expired,

        // وضعیت‌های نامشخص
        ["UNKNOWN"] = PaymentStatusEnum.SubmittedToBank
    };

    /// <summary>
    /// نقشه تبدیل وضعیت تراکنش‌های فردی
    /// </summary>
    private static readonly Dictionary<string, PaymentItemStatusEnum> TransactionStatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // وضعیت‌های در انتظار
        ["TODO"] = PaymentItemStatusEnum.WaitForExecution,
        ["REGISTERED"] = PaymentItemStatusEnum.Registered,

        // وضعیت‌های در حال پردازش
        ["INPROGRESS"] = PaymentItemStatusEnum.WaitForBank,
        ["PROCESSING"] = PaymentItemStatusEnum.WaitForBank,

        // وضعیت‌های موفق
        ["DONE"] = PaymentItemStatusEnum.BankSucceeded,
        ["SUCCESS"] = PaymentItemStatusEnum.BankSucceeded,
        ["SUCCESSFUL"] = PaymentItemStatusEnum.BankSucceeded,

        // وضعیت‌های ناموفق
        ["FAIL"] = PaymentItemStatusEnum.BankRejected,
        ["FAILED"] = PaymentItemStatusEnum.BankRejected,
        ["ERROR"] = PaymentItemStatusEnum.BankRejected,
        ["REJECTED"] = PaymentItemStatusEnum.BankRejected,

        // وضعیت‌های برگشت
        ["ROLLBACK"] = PaymentItemStatusEnum.TransactionRollback,
        ["REVERSED"] = PaymentItemStatusEnum.TransactionRollback,
        ["REFUNDED"] = PaymentItemStatusEnum.TransactionRollback,

        // وضعیت‌های لغو
        ["CANCELED"] = PaymentItemStatusEnum.Canceled,
        ["CANCELLED"] = PaymentItemStatusEnum.Canceled,

        // وضعیت‌های منقضی
        ["EXPIRED"] = PaymentItemStatusEnum.Expired,
        ["TIMEOUT"] = PaymentItemStatusEnum.Expired,

        // وضعیت‌های نامشخص
        ["UNKNOWN"] = PaymentItemStatusEnum.WaitForBank,
        [""] = PaymentItemStatusEnum.WaitForBank
    };

    /// <summary>
    /// تبدیل وضعیت بانک به وضعیت سفارش
    /// </summary>
    public static PaymentStatusEnum MapOrderStatus(string bankStatus)
    {
        if (string.IsNullOrEmpty(bankStatus))
            return PaymentStatusEnum.SubmittedToBank;

        return OrderStatusMap.TryGetValue(bankStatus.Trim(), out var status)
            ? status
            : PaymentStatusEnum.SubmittedToBank;
    }

    /// <summary>
    /// تبدیل وضعیت بانک به وضعیت تراکنش
    /// </summary>
    public static PaymentItemStatusEnum MapTransactionStatus(string bankStatus)
    {
        if (string.IsNullOrEmpty(bankStatus))
            return PaymentItemStatusEnum.WaitForBank;

        return TransactionStatusMap.TryGetValue(bankStatus.Trim(), out var status)
            ? status
            : PaymentItemStatusEnum.WaitForBank;
    }

    /// <summary>
    /// آیا وضعیت نهایی است
    /// </summary>
    public static bool IsFinalStatus(string bankStatus)
    {
        if (string.IsNullOrEmpty(bankStatus))
            return false;

        var finalStatuses = new[]
        {
            "DONE", "FAILED", "FAIL", "CANCELED", "CANCELLED", "EXPIRED"
        };

        return finalStatuses.Contains(bankStatus.ToUpper());
    }

    /// <summary>
    /// آیا وضعیت موفق است
    /// </summary>
    public static bool IsSuccessStatus(string bankStatus)
    {
        if (string.IsNullOrEmpty(bankStatus))
            return false;

        var successStatuses = new[] { "DONE", "SUCCESS", "SUCCESSFUL" };
        return successStatuses.Contains(bankStatus.ToUpper());
    }

    /// <summary>
    /// آیا وضعیت خطا است
    /// </summary>
    public static bool IsErrorStatus(string bankStatus)
    {
        if (string.IsNullOrEmpty(bankStatus))
            return false;

        var errorStatuses = new[]
        {
            "GROUP_PAYMENT_ERROR_STATE", "FAILED", "FAIL", "ERROR", "REJECTED"
        };

        return errorStatuses.Contains(bankStatus.ToUpper());
    }

    /// <summary>
    /// آیا آماده اجرا است
    /// </summary>
    public static bool IsReadyForExecution(string bankStatus)
    {
        return string.Equals(bankStatus?.Trim(), "READY", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// آیا نیاز به استعلام تفصیلی دارد
    /// </summary>
    public static bool RequiresDetailedInquiry(string bankStatus)
    {
        if (string.IsNullOrEmpty(bankStatus))
            return false;

        var detailedInquiryStatuses = new[] { "DONE", "FAILED", "FAIL" };
        return detailedInquiryStatuses.Contains(bankStatus.ToUpper());
    }

    /// <summary>
    /// دریافت توضیح فارسی وضعیت
    /// </summary>
    public static string GetStatusDescription(string bankStatus)
    {
        if (string.IsNullOrEmpty(bankStatus))
            return "نامشخص";

        return bankStatus.ToUpper() switch
        {
            "GROUP_PAYMENT_WAITING_STATE" => "در انتظار پردازش",
            "GROUP_PAYMENT_REGISTERED_STATE" => "ثبت شده",
            "GROUP_PAYMENT_UPLOADING_STATE" => "در حال آپلود",
            "GROUP_PAYMENT_ERROR_STATE" => "خطا در پردازش",
            "PROCESSING" => "در حال پردازش",
            "READY" => "آماده اجرا",
            "DONE" => "تکمیل شده",
            "FAILED" or "FAIL" => "ناموفق",
            "CANCELED" or "CANCELLED" => "لغو شده",
            "EXPIRED" => "منقضی شده",
            "TODO" => "در انتظار اجرا",
            "INPROGRESS" => "در حال اجرا",
            "REGISTERED" => "ثبت شده",
            "ROLLBACK" or "REVERSED" => "برگشت خورده",
            _ => bankStatus
        };
    }

    /// <summary>
    /// تعیین وضعیت نهایی سفارش بر اساس وضعیت تراکنش‌ها
    /// </summary>
    public static PaymentStatusEnum DetermineOrderStatusFromTransactions(IEnumerable<PaymentItemStatusEnum> transactionStatuses)
    {
        var statuses = transactionStatuses.ToList();

        if (!statuses.Any())
            return PaymentStatusEnum.SubmittedToBank;

        // اگر همه موفق
        if (statuses.All(s => s == PaymentItemStatusEnum.BankSucceeded))
            return PaymentStatusEnum.BankSucceeded;

        // اگر همه ناموفق
        if (statuses.All(s => s == PaymentItemStatusEnum.BankRejected))
            return PaymentStatusEnum.BankRejected;

        // اگر ترکیبی از موفق و ناموفق
        if (statuses.Any(s => s == PaymentItemStatusEnum.BankSucceeded) &&
            statuses.Any(s => s == PaymentItemStatusEnum.BankRejected))
            return PaymentStatusEnum.DoneWithError;

        // اگر همه لغو شده
        if (statuses.All(s => s == PaymentItemStatusEnum.Canceled))
            return PaymentStatusEnum.Canceled;

        // در سایر موارد، هنوز در حال پردازش
        return PaymentStatusEnum.SubmittedToBank;
    }

    /// <summary>
    /// آیا وضعیت قابل تکرار است (برای retry logic)
    /// </summary>
    public static bool IsRetryableStatus(string bankStatus)
    {
        if (string.IsNullOrEmpty(bankStatus))
            return true; // وضعیت نامشخص قابل تکرار است

        var nonRetryableStatuses = new[]
        {
            "DONE", "FAILED", "FAIL", "CANCELED", "CANCELLED", "EXPIRED",
            "GROUP_PAYMENT_ERROR_STATE"
        };

        return !nonRetryableStatuses.Contains(bankStatus.ToUpper());
    }
}