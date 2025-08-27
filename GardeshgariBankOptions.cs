namespace TPG.SI.TadbirPay.Withdrawal.TourismBank;

/// <summary>
/// تنظیمات کامل بانک گردشگری - نسخه بهبود یافته
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
    /// حداکثر تعداد تلاش برای Execute - فقط برای خطاهای فنی
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

    #region Safe Retry Configuration

    /// <summary>
    /// حداکثر تعداد تلاش برای عملیات استعلام (فقط read-only operations)
    /// </summary>
    public int MaxInquiryRetryAttempts { get; set; } = 3;

    /// <summary>
    /// زمان انتظار بین استعلام‌های ناموفق (ثانیه)
    /// </summary>
    public int InquiryRetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// کدهای خطای قابل تکرار برای Execute
    /// </summary>
    public int[] RetryableExecutionErrorCodes { get; set; } = { 408, 500, 502, 503, 504 };

    /// <summary>
    /// کدهای خطای قابل تکرار برای Inquiry
    /// </summary>
    public int[] RetryableInquiryErrorCodes { get; set; } = { 408, 500, 502, 503, 504, 429 };

    /// <summary>
    /// حداکثر زمان انتظار برای refresh استعلام (دقیقه)
    /// </summary>
    public int InquiryRefreshIntervalMinutes { get; set; } = 5;

    #endregion

    #region Performance Configuration

    /// <summary>
    /// فعال‌سازی cache برای نتایج استعلام موفق
    /// </summary>
    public bool EnableInquiryCache { get; set; } = true;

    /// <summary>
    /// مدت زمان نگهداری cache استعلام (دقیقه)
    /// </summary>
    public int InquiryCacheDurationMinutes { get; set; } = 10;

    /// <summary>
    /// حداکثر تعداد درخواست همزمان
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// اندازه batch برای استعلام‌های تکی (تعداد تراکنش در هر درخواست)
    /// </summary>
    public int BatchInquirySize { get; set; } = 50;

    #endregion

    #region Security Configuration

    /// <summary>
    /// فعال‌سازی اعتبارسنجی certificate
    /// </summary>
    public bool EnableCertificateValidation { get; set; } = true;

    /// <summary>
    /// فعال‌سازی signature verification
    /// </summary>
    public bool EnableSignatureVerification { get; set; } = true;

    /// <summary>
    /// حداکثر اندازه پاسخ مجاز (بایت)
    /// </summary>
    public long MaxResponseSizeBytes { get; set; } = 10_485_760; // 10MB

    #endregion

    #region Monitoring Configuration

    /// <summary>
    /// فعال‌سازی metrics
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// فعال‌سازی health checks
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// فاصله زمانی health check (ثانیه)
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 300; // 5 minutes

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

    /// <summary>
    /// آیا retry برای Execute امکان‌پذیر است
    /// </summary>
    public bool IsExecutionRetryEnabled => MaxExecutionAttempts > 1;

    /// <summary>
    /// آیا retry برای Inquiry امکان‌پذیر است
    /// </summary>
    public bool IsInquiryRetryEnabled => MaxInquiryRetryAttempts > 1;

    /// <summary>
    /// TimeSpan برای refresh interval استعلام
    /// </summary>
    public TimeSpan InquiryRefreshInterval => TimeSpan.FromMinutes(InquiryRefreshIntervalMinutes);

    /// <summary>
    /// TimeSpan برای cache duration
    /// </summary>
    public TimeSpan InquiryCacheDuration => TimeSpan.FromMinutes(InquiryCacheDurationMinutes);

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
               ReadinessCheckIntervalSeconds > 0 &&
               MaxInquiryRetryAttempts >= 0 &&
               InquiryRetryDelaySeconds > 0 &&
               InquiryRefreshIntervalMinutes > 0 &&
               MaxConcurrentRequests > 0 &&
               BatchInquirySize > 0 &&
               MaxResponseSizeBytes > 0;
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

        if (MaxInquiryRetryAttempts < 0)
            errors.Add("MaxInquiryRetryAttempts نمی‌تواند منفی باشد");

        if (InquiryRetryDelaySeconds <= 0)
            errors.Add("InquiryRetryDelaySeconds باید بزرگتر از صفر باشد");

        if (InquiryRefreshIntervalMinutes <= 0)
            errors.Add("InquiryRefreshIntervalMinutes باید بزرگتر از صفر باشد");

        if (MaxConcurrentRequests <= 0)
            errors.Add("MaxConcurrentRequests باید بزرگتر از صفر باشد");

        if (BatchInquirySize <= 0)
            errors.Add("BatchInquirySize باید بزرگتر از صفر باشد");

        if (MaxResponseSizeBytes <= 0)
            errors.Add("MaxResponseSizeBytes باید بزرگتر از صفر باشد");

        // بررسی محدودیت‌های منطقی
        if (MaxExecutionAttempts > 10)
            errors.Add("MaxExecutionAttempts نباید بیشتر از 10 باشد (خطر پرداخت دوبل)");

        if (RetryDelayMilliseconds > 30000)
            errors.Add("RetryDelayMilliseconds نباید بیشتر از 30 ثانیه باشد");

        if (MaxWaitForReadyMinutes > 120)
            errors.Add("MaxWaitForReadyMinutes نباید بیشتر از 2 ساعت باشد");

        if (TimeoutSeconds > 600)
            errors.Add("TimeoutSeconds نباید بیشتر از 10 دقیقه باشد");

        return errors;
    }

    /// <summary>
    /// آیا کد خطا برای Execute قابل تکرار است
    /// </summary>
    public bool IsExecutionErrorRetryable(int? errorCode)
    {
        return errorCode.HasValue &&
               IsExecutionRetryEnabled &&
               RetryableExecutionErrorCodes.Contains(errorCode.Value);
    }

    /// <summary>
    /// آیا کد خطا برای Inquiry قابل تکرار است
    /// </summary>
    public bool IsInquiryErrorRetryable(int? errorCode)
    {
        return errorCode.HasValue &&
               IsInquiryRetryEnabled &&
               RetryableInquiryErrorCodes.Contains(errorCode.Value);
    }

    #endregion

    #region Environment Specific Configurations

    /// <summary>
    /// تنظیمات محیط Development
    /// </summary>
    public static GardeshgariBankOptions ForDevelopment()
    {
        return new GardeshgariBankOptions
        {
            BaseApiUrl = "https://test-gsb.TourismBank.ir",
            AccessTokenUrl = "https://test-sso.TourismBank.ir/oauth/token",
            TimeoutSeconds = 30,
            MaxExecutionAttempts = 1, // کم‌تر در dev
            RetryDelayMilliseconds = 1000,
            EnableDetailedLogging = true,
            MaxInquiryRetryAttempts = 1,
            EnableInquiryCache = false, // بدون cache در dev
            EnableCertificateValidation = false
        };
    }

    /// <summary>
    /// تنظیمات محیط Production
    /// </summary>
    public static GardeshgariBankOptions ForProduction()
    {
        return new GardeshgariBankOptions
        {
            BaseApiUrl = "https://gsb.TourismBank.ir",
            AccessTokenUrl = "https://sso.TourismBank.ir/oauth/token",
            TimeoutSeconds = 120,
            MaxExecutionAttempts = 3,
            RetryDelayMilliseconds = 5000,
            EnableDetailedLogging = false,
            MaxInquiryRetryAttempts = 3,
            EnableInquiryCache = true,
            EnableCertificateValidation = true,
            EnableMetrics = true,
            EnableHealthChecks = true
        };
    }

    #endregion
}