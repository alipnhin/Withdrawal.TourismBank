namespace TPG.SI.TadbirPay.Withdrawal.TourismBank;

/// <summary>
/// تنظیمات کامل بانک گردشگری
/// </summary>
public class GardeshgariBankOptions
{
    #region API Configuration

    /// <summary>
    /// آدرس پایه API بانک گردشگری
    /// </summary>
    public string BaseApiUrl { get; set; } = "https://gsb.TourismBank.ir";

    /// <summary>
    /// آدرس دریافت توکن
    /// </summary>
    public string AccessTokenUrl { get; set; } = "https://sso.TourismBank.ir/oauth/token";

    /// <summary>
    /// نسخه API
    /// </summary>
    public string ApiVersion { get; set; } = "7";

    /// <summary>
    /// مدت زمان انتظار برای درخواست (ثانیه)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    #endregion

    #region Payment Workflow Configuration

    /// <summary>
    /// حداکثر تعداد تلاش برای Execute
    /// </summary>
    public int MaxExecutionAttempts { get; set; } = 3;

    /// <summary>
    /// زمان انتظار بین تلاش‌ها (میلی‌ثانیه)
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 5000;

    /// <summary>
    /// فعال‌سازی لاگ‌های تفصیلی
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// زمان انتظار برای درخواست‌های HTTP (ثانیه) - اگر مشخص نشده از TimeoutSeconds استفاده می‌شود
    /// </summary>
    public int? HttpTimeoutSeconds { get; set; }

    /// <summary>
    /// حداکثر زمان انتظار برای Ready شدن تراکنش (دقیقه)
    /// </summary>
    public int MaxWaitForReadyMinutes { get; set; } = 30;

    /// <summary>
    /// فاصله زمانی بین استعلام‌های آمادگی (ثانیه)
    /// </summary>
    public int ReadinessCheckIntervalSeconds { get; set; } = 10;

    #endregion

    #region Helper Properties

    /// <summary>
    /// زمان انتظار HTTP محاسبه شده
    /// </summary>
    public int EffectiveHttpTimeout => HttpTimeoutSeconds ?? TimeoutSeconds;

    /// <summary>
    /// آیا لاگ‌های تفصیلی فعال است
    /// </summary>
    public bool IsDetailedLoggingEnabled => EnableDetailedLogging;

    #endregion

    #region Validation

    /// <summary>
    /// اعتبارسنجی تنظیمات
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(BaseApiUrl) &&
               !string.IsNullOrEmpty(AccessTokenUrl) &&
               !string.IsNullOrEmpty(ApiVersion) &&
               TimeoutSeconds > 0 &&
               MaxExecutionAttempts > 0 &&
               RetryDelayMilliseconds > 0 &&
               MaxWaitForReadyMinutes > 0 &&
               ReadinessCheckIntervalSeconds > 0;
    }

    /// <summary>
    /// دریافت خطاهای اعتبارسنجی
    /// </summary>
    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(BaseApiUrl))
            errors.Add("BaseApiUrl نمی‌تواند خالی باشد");

        if (string.IsNullOrEmpty(AccessTokenUrl))
            errors.Add("AccessTokenUrl نمی‌تواند خالی باشد");

        if (string.IsNullOrEmpty(ApiVersion))
            errors.Add("ApiVersion نمی‌تواند خالی باشد");

        if (TimeoutSeconds <= 0)
            errors.Add("TimeoutSeconds باید بزرگتر از صفر باشد");

        if (MaxExecutionAttempts <= 0)
            errors.Add("MaxExecutionAttempts باید بزرگتر از صفر باشد");

        if (RetryDelayMilliseconds <= 0)
            errors.Add("RetryDelayMilliseconds باید بزرگتر از صفر باشد");

        if (MaxWaitForReadyMinutes <= 0)
            errors.Add("MaxWaitForReadyMinutes باید بزرگتر از صفر باشد");

        if (ReadinessCheckIntervalSeconds <= 0)
            errors.Add("ReadinessCheckIntervalSeconds باید بزرگتر از صفر باشد");

        return errors;
    }

    #endregion
}