using System.Text.Json;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Payment;

/// <summary>
/// مدل metadata برای مدیریت state پرداخت گروهی - نسخه بازطراحی شده
/// </summary>
public class PaymentOrderMetadata
{
    /// <summary>
    /// مرحله فعلی پرداخت
    /// </summary>
    public PaymentPhase CurrentPhase { get; set; } = PaymentPhase.Registered;

    /// <summary>
    /// آخرین وضعیت دریافت شده از بانک
    /// </summary>
    public string LastBankStatus { get; set; }

    /// <summary>
    /// وضعیت هر تراکنش بر اساس شماره ردیف
    /// </summary>
    public Dictionary<int, string> TransactionStatuses { get; set; } = new();

    /// <summary>
    /// آخرین زمان استعلام از بانک
    /// </summary>
    public DateTime? LastInquiryTime { get; set; }

    /// <summary>
    /// تعداد دفعات تلاش برای Execute
    /// </summary>
    public int ExecutionAttempts { get; set; } = 0;

    /// <summary>
    /// آخرین پیام خطا در مرحله Execute
    /// </summary>
    public string LastExecutionError { get; set; }

    /// <summary>
    /// آیا نیاز به استعلام تفصیلی دارد
    /// </summary>
    public bool RequiresDetailedInquiry { get; set; } = false;

    /// <summary>
    /// زمان شروع مرحله Execute
    /// </summary>
    public DateTime? ExecutionStartedAt { get; set; }

    /// <summary>
    /// زمان تکمیل مرحله Execute
    /// </summary>
    public DateTime? ExecutionCompletedAt { get; set; }

    /// <summary>
    /// آیا DoPayment با موفقیت اجرا شده است
    /// </summary>
    public bool IsDoPaymentCompleted { get; set; } = false;

    /// <summary>
    /// تبدیل به JSON برای ذخیره در PaymentOrderDto.MetaData
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    /// <summary>
    /// ایجاد از JSON
    /// </summary>
    public static PaymentOrderMetadata FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return new PaymentOrderMetadata();

        try
        {
            return JsonSerializer.Deserialize<PaymentOrderMetadata>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            }) ?? new PaymentOrderMetadata();
        }
        catch
        {
            return new PaymentOrderMetadata();
        }
    }

    /// <summary>
    /// آیا نیاز به اجرای DoPayment دارد
    /// </summary>
    public bool ShouldExecuteDoPayment()
    {
        return LastBankStatus?.ToUpper() == "READY" &&
               !IsDoPaymentCompleted &&
               ExecutionAttempts < 3;
    }

    /// <summary>
    /// آیا باید از استعلام تفصیلی استفاده کند
    /// </summary>
    public bool ShouldUseDetailedInquiry()
    {
        return IsDoPaymentCompleted ||
               RequiresDetailedInquiry ||
               LastBankStatus?.ToUpper() == "DONE" ||
               CurrentPhase >= PaymentPhase.Executed;
    }

    /// <summary>
    /// به‌روزرسانی metadata بر اساس وضعیت بانک
    /// </summary>
    public void UpdateFromBankStatus(string bankStatus)
    {
        LastBankStatus = bankStatus;
        LastInquiryTime = DateTime.UtcNow;

        var status = bankStatus?.ToUpper();
        switch (status)
        {
            case "READY":
                CurrentPhase = PaymentPhase.ReadyForExecution;
                break;
            case "DONE":
                CurrentPhase = PaymentPhase.Executed;
                RequiresDetailedInquiry = true;
                break;
            case "GROUP_PAYMENT_ERROR_STATE":
            case "FAILED":
                CurrentPhase = PaymentPhase.Failed;
                break;
            case "GROUP_PAYMENT_WAITING_STATE":
            case "GROUP_PAYMENT_REGISTERED_STATE":
            case "GROUP_PAYMENT_UPLOADING_STATE":
                CurrentPhase = PaymentPhase.Processing;
                break;
        }
    }

    /// <summary>
    /// به‌روزرسانی وضعیت یک تراکنش
    /// </summary>
    public void UpdateTransactionStatus(int lineNumber, string status)
    {
        TransactionStatuses[lineNumber] = status;
    }

    /// <summary>
    /// علامت‌گذاری شروع مرحله Execute
    /// </summary>
    public void MarkExecutionStarted()
    {
        ExecutionStartedAt = DateTime.UtcNow;
        ExecutionAttempts++;
        CurrentPhase = PaymentPhase.Executing;
    }

    /// <summary>
    /// علامت‌گذاری تکمیل موفق مرحله Execute
    /// </summary>
    public void MarkExecutionCompleted()
    {
        IsDoPaymentCompleted = true;
        ExecutionCompletedAt = DateTime.UtcNow;
        LastExecutionError = null;
        CurrentPhase = PaymentPhase.Executed;
    }

    /// <summary>
    /// علامت‌گذاری خطا در مرحله Execute
    /// </summary>
    public void MarkExecutionFailed(string error)
    {
        LastExecutionError = error;
        ExecutionCompletedAt = DateTime.UtcNow;
        CurrentPhase = PaymentPhase.Failed;
    }

    /// <summary>
    /// آیا تراکنش در حالت نهایی است
    /// </summary>
    public bool IsInFinalState()
    {
        return CurrentPhase == PaymentPhase.Completed ||
               CurrentPhase == PaymentPhase.Failed;
    }

    /// <summary>
    /// دریافت توضیح وضعیت فعلی
    /// </summary>
    public string GetCurrentStatusDescription()
    {
        return CurrentPhase switch
        {
            PaymentPhase.Registered => "ثبت شده در بانک",
            PaymentPhase.Processing => "در حال پردازش",
            PaymentPhase.ReadyForExecution => "آماده اجرا",
            PaymentPhase.Executing => "در حال اجرا",
            PaymentPhase.Executed => "اجرا شده",
            PaymentPhase.Completed => "تکمیل شده",
            PaymentPhase.Failed => "ناموفق",
            _ => "نامشخص"
        };
    }
}

/// <summary>
/// مراحل مختلف پرداخت گروهی
/// </summary>
public enum PaymentPhase
{
    /// <summary>
    /// ثبت اولیه در بانک
    /// </summary>
    Registered = 0,

    /// <summary>
    /// در حال پردازش در بانک
    /// </summary>
    Processing = 1,

    /// <summary>
    /// آماده اجرای DoPayment
    /// </summary>
    ReadyForExecution = 2,

    /// <summary>
    /// در حال اجرای DoPayment
    /// </summary>
    Executing = 3,

    /// <summary>
    /// DoPayment اجرا شده
    /// </summary>
    Executed = 4,

    /// <summary>
    /// تمام مراحل تکمیل شده
    /// </summary>
    Completed = 5,

    /// <summary>
    /// با خطا مواجه شده
    /// </summary>
    Failed = -1
}