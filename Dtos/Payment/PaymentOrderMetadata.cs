using System.Text.Json;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Payment;

/// <summary>
/// مدل metadata برای مدیریت state پرداخت گروهی
/// </summary>
public class PaymentOrderMetadata
{
    /// <summary>
    /// آیا مرحله Execute شروع شده است
    /// </summary>
    public bool IsExecutionStarted { get; set; } = false;

    /// <summary>
    /// آیا مرحله Execute با موفقیت انجام شده است
    /// </summary>
    public bool IsExecutionCompleted { get; set; } = false;

    /// <summary>
    /// تاریخ شروع مرحله Execute
    /// </summary>
    public DateTime? ExecutionStartedAt { get; set; }

    /// <summary>
    /// تاریخ تکمیل مرحله Execute
    /// </summary>
    public DateTime? ExecutionCompletedAt { get; set; }

    /// <summary>
    /// تعداد دفعات تلاش برای Execute
    /// </summary>
    public int ExecutionAttempts { get; set; } = 0;

    /// <summary>
    /// آخرین پیام خطا در مرحله Execute
    /// </summary>
    public string LastExecutionError { get; set; }

    /// <summary>
    /// وضعیت آخرین استعلام از بانک
    /// </summary>
    public string LastBankStatus { get; set; }

    /// <summary>
    /// تاریخ آخرین استعلام
    /// </summary>
    public DateTime? LastInquiryAt { get; set; }

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
    /// علامت‌گذاری شروع مرحله Execute
    /// </summary>
    public void MarkExecutionStarted()
    {
        IsExecutionStarted = true;
        ExecutionStartedAt = DateTime.UtcNow;
        ExecutionAttempts++;
    }

    /// <summary>
    /// علامت‌گذاری تکمیل موفق مرحله Execute
    /// </summary>
    public void MarkExecutionCompleted()
    {
        IsExecutionCompleted = true;
        ExecutionCompletedAt = DateTime.UtcNow;
        LastExecutionError = null;
    }

    /// <summary>
    /// علامت‌گذاری خطا در مرحله Execute
    /// </summary>
    public void MarkExecutionFailed(string error)
    {
        LastExecutionError = error;
        ExecutionCompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// به‌روزرسانی وضعیت استعلام
    /// </summary>
    public void UpdateInquiryStatus(string bankStatus)
    {
        LastBankStatus = bankStatus;
        LastInquiryAt = DateTime.UtcNow;
    }

    /// <summary>
    /// آیا نیاز به Execute دارد
    /// </summary>
    public bool NeedsExecution()
    {
        return !IsExecutionStarted || !IsExecutionCompleted && ExecutionAttempts < 3;
    }

    /// <summary>
    /// آیا باید از سرویس InquiryFromCore استفاده کند
    /// </summary>
    public bool ShouldUseInquiryFromCore()
    {
        return IsExecutionStarted;
    }
}