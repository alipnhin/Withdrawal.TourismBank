# نقشه راه اصلاح ماژول بانک گردشگری TadbirPay

## خلاصه وضعیت فعلی

**پروژه**: ماژول پرداخت گروهی بانک گردشگری در سیستم TadbirPay
**مشکل اصلی**: عدم پشتیبانی از تمامی حالت‌ها و سناریوها + مدیریت نادرست metadata در فرآیند پرداخت
**هدف**: اصلاح کامل فلوی پرداخت مطابق مستندات بانک و پیاده‌سازی مدیریت صحیح metadata

## معماری کنونی

### کامپوننت‌های اصلی:
- **PaymentService**: ارتباط با API بانک
- **GardeshgariPaymentWorkflow**: مدیریت منطق فرآیند
- **GardeshgariPaymentProviderService**: نقطه ورودی اصلی
- **PaymentOrderMetadata**: مدیریت state (ناقص)

### فرآیند فعلی:
1. `RegisterGroupPaymentAsync()` - ثبت
2. `CheckExecutionReadinessAsync()` - بررسی آمادگی (ناقص)
3. `ExecuteGroupPaymentAsync()` - اجرا (ناقص)
4. `InquiryTransactionDetailsAsync()` - استعلام

## مشکلات شناسایی شده

### 1. مدیریت Metadata ناکامل
```csharp
// مشکل فعلی
public class PaymentOrderMetadata
{
    public bool IsExecutionStarted { get; set; } = false;
    public bool IsExecutionCompleted { get; set; } = false;
    // فیلدهای محدود و منطق ناقص
}
```

**مشکلات:**
- عدم پیگیری وضعیت تراکنش‌های تکی
- عدم تطبیق با وضعیت‌های بانک
- منطق تصمیم‌گیری نادرست برای انتخاب API

### 2. فلوی ناقص مطابق مستندات بانک

**مستندات بانک:**
- **API 1**: `GroupPaymentRegister` - ثبت اولیه
- **API 2**: `GroupPaymentInquiry` - بررسی آمادگی
- **API 3**: `DoPayment` - اجرای نهایی (فقط زمانی که وضعیت READY است)
- **API 4**: `GroupPaymentInquiryFromCore` - استعلام جزئیات

**وضعیت‌های بانک:**
```
GROUP_PAYMENT_WAITING_STATE
GROUP_PAYMENT_REGISTERED_STATE  
GROUP_PAYMENT_UPLOADING_STATE
GROUP_PAYMENT_ERROR_STATE
READY
DONE
FAILED
```

### 3. منطق استعلام نادرست
```csharp
// منطق فعلی نادرست
if (metadata.NeedsExecution())
{
    // تصمیم بر اساس metadata داخلی، نه وضعیت بانک
}
```

**باید باشد:**
```csharp
// تصمیم بر اساس وضعیت دریافت شده از بانک
switch (bankStatus)
{
    case "READY": // اجرای DoPayment
    case "DONE": // استعلام تفصیلی
    // ...
}
```

### 4. عدم پشتیبانی از سناریوهای مختلف
- سناریو پرداخت ترکیبی (INTERNAL + PAYA + SATNA)
- مدیریت خطاهای مرحله‌ای
- retry logic برای حالات موقت
- مدیریت timeout ها

## نقشه راه اصلاحات

### فاز 1: اصلاح مدیریت Metadata

#### 1.1 بازطراحی PaymentOrderMetadata
```csharp
public class PaymentOrderMetadata
{
    public PaymentPhase CurrentPhase { get; set; } = PaymentPhase.Registered;
    public string LastBankStatus { get; set; }
    public Dictionary<int, TransactionStatus> TransactionStatuses { get; set; } = new();
    public DateTime? LastInquiryTime { get; set; }
    public int ExecutionAttempts { get; set; } = 0;
    public string LastExecutionError { get; set; }
    public bool RequiresDetailedInquiry { get; set; }
    
    // Methods
    public bool ShouldExecuteDoPayment()
    public bool ShouldUseDetailedInquiry()
    public void UpdateFromBankStatus(string bankStatus)
    public void UpdateTransactionStatus(int lineNumber, string status)
}

public enum PaymentPhase
{
    Registered,
    ReadyForExecution,
    Executed,
    Completed,
    Failed
}
```

#### 1.2 اصلاح Extensions
```csharp
public static class PaymentOrderDtoExtensions
{
    // حفظ متدهای فعلی + اضافه کردن:
    public static void UpdateTransactionStatuses(this PaymentOrderDto order, TransactionInquiryResult result)
    public static bool RequiresExecution(this PaymentOrderDto order)
    public static void MarkPhaseCompleted(this PaymentOrderDto order, PaymentPhase phase)
}
```

### فاز 2: اصلاح Workflow منطق

#### 2.1 بازنویسی InquiryPaymentAsync
```csharp
public async Task<ProviderResponseDto<PaymentOrderDto>> InquiryPaymentAsync(
    PaymentOrderDto paymentOrder, GatewayInfoDto gatewayInfo)
{
    // 1. همیشه وضعیت کلی را از بانک بگیر
    var readinessResult = await CheckExecutionReadinessAsync(...);
    
    // 2. metadata را بر اساس وضعیت بانک به‌روزرسانی کن
    paymentOrder.UpdateFromBankStatus(readinessResult.TransactionStatus);
    
    // 3. تصمیم‌گیری بر اساس وضعیت بانک
    return readinessResult.TransactionStatus?.ToUpper() switch
    {
        "READY" => await HandleReadyStateAsync(paymentOrder, gatewayInfo),
        "DONE" => await HandleDoneStateAsync(paymentOrder, gatewayInfo),
        "GROUP_PAYMENT_ERROR_STATE" => HandleErrorState(paymentOrder, readinessResult),
        _ => HandlePendingState(paymentOrder, readinessResult.TransactionStatus)
    };
}
```

#### 2.2 اضافه کردن Handler Methods
```csharp
private async Task<ProviderResponseDto<PaymentOrderDto>> HandleReadyStateAsync(...)
{
    if (paymentOrder.RequiresExecution())
    {
        return await ExecutePaymentWithRetryAsync(paymentOrder, gatewayInfo);
    }
    return await PerformDetailedInquiryAsync(paymentOrder, gatewayInfo);
}

private async Task<ProviderResponseDto<PaymentOrderDto>> HandleDoneStateAsync(...)
{
    return await PerformDetailedInquiryAsync(paymentOrder, gatewayInfo);
}

private async Task<ProviderResponseDto<PaymentOrderDto>> ExecutePaymentWithRetryAsync(...)
{
    // پیاده‌سازی retry logic
    // مدیریت timeout
    // به‌روزرسانی metadata
}
```

### فاز 3: بهبود مدیریت خطا و Resilience (بدون Circuit Breaker)

#### 3.1 اضافه کردن Retry Policy فقط برای عملیات Safe
```csharp
public class GardeshgariBankOptions
{
    // فیلدهای فعلی +
    public int MaxInquiryRetryAttempts { get; set; } = 3; // فقط برای inquiry
    public int RetryDelaySeconds { get; set; } = 5;
    public int MaxWaitForReadyMinutes { get; set; } = 30;
    public string[] RetryableInquiryErrors { get; set; } = { "TIMEOUT", "CONNECTION_ERROR" };
    
    // هرگز retry برای register/execute نباشد
    public bool EnablePaymentRetry { get; set; } = false; // همیشه false
}
```

#### 3.2 مدیریت خطای هوشمند (بدون Circuit Breaker)
```csharp
public class SafePaymentService : IPaymentService
{
    private readonly IPaymentService _innerService;
    private readonly ILogger _logger;
    
    // فقط retry برای عملیات read-only مثل inquiry
    // هرگز retry برای write operations مثل register/execute
    public async Task<T> ExecuteWithSafeRetryAsync<T>(
        Func<Task<T>> operation, 
        bool isIdempotent)
    {
        if (isIdempotent)
        {
            // فقط عملیات inquiry قابل تکرار است
            return await RetryPolicy.ExecuteAsync(operation);
        }
        
        // عملیات پرداخت فقط یک بار اجرا می‌شود
        return await operation();
    }
}
```

**⚠️ اصل مهم: Circuit Breaker در سیستم‌های پرداخت ممنوع**
- خطر پرداخت دوبل
- عدم قطعیت در وضعیت تراکنش
- ممکن است کاربر را گمراه کند

### فاز 4: اصلاح Status Mapping

#### 4.1 بهبود Status Mapping
```csharp
public static class BankStatusMapper
{
    private static readonly Dictionary<string, PaymentStatusEnum> OrderStatusMap = new()
    {
        ["GROUP_PAYMENT_WAITING_STATE"] = PaymentStatusEnum.SubmittedToBank,
        ["GROUP_PAYMENT_REGISTERED_STATE"] = PaymentStatusEnum.SubmittedToBank,
        ["READY"] = PaymentStatusEnum.SubmittedToBank,
        ["DONE"] = PaymentStatusEnum.BankSucceeded, // or requires detailed check
        ["GROUP_PAYMENT_ERROR_STATE"] = PaymentStatusEnum.BankRejected,
        ["FAILED"] = PaymentStatusEnum.BankRejected
    };
    
    private static readonly Dictionary<string, PaymentItemStatusEnum> TransactionStatusMap = new()
    {
        ["TODO"] = PaymentItemStatusEnum.WaitForExecution,
        ["INPROGRESS"] = PaymentItemStatusEnum.WaitForBank,
        ["DONE"] = PaymentItemStatusEnum.BankSucceeded,
        ["FAIL"] = PaymentItemStatusEnum.BankRejected,
        ["CANCELED"] = PaymentItemStatusEnum.Canceled
    };
}
```

### فاز 5: اضافه کردن Tests و Monitoring

#### 5.1 Unit Tests
```csharp
[TestFixture]
public class PaymentWorkflowTests
{
    [Test]
    public async Task InquiryPaymentAsync_WhenReady_ShouldExecuteDoPayment()
    
    [Test] 
    public async Task InquiryPaymentAsync_WhenDone_ShouldPerformDetailedInquiry()
    
    [Test]
    public async Task Metadata_ShouldPersistBetweenCalls()
    
    [Test]
    public async Task RetryLogic_ShouldWorkCorrectly()
}
```

#### 5.2 Integration Tests
```csharp
[TestFixture]
public class BankIntegrationTests
{
    [Test]
    public async Task CompleteFlow_RegisterToCompletion_ShouldWork()
    
    [Test]
    public async Task ErrorScenarios_ShouldBeHandledCorrectly()
}
```

## اولویت‌بندی پیاده‌سازی

### Priority 1 (Critical):
1. اصلاح PaymentOrderMetadata
2. بازنویسی InquiryPaymentAsync logic
3. اضافه کردن proper status mapping

### Priority 2 (High):
1. پیاده‌سازی safe retry mechanism (فقط برای inquiry)
2. بهبود error handling
3. اضافه کردن comprehensive logging

### Priority 3 (Medium):
1. اضافه کردن timeout management
2. performance optimizations
3. monitoring و metrics (بدون circuit breaker)

### Priority 4 (Nice to have):
1. comprehensive test coverage
2. documentation updates
3. health checks improvement

## ملاحظات پیاده‌سازی

### Breaking Changes:
- PaymentOrderMetadata structure تغییر می‌کند
- ممکن است نیاز به migration باشد
- API contracts تغییر نمی‌کنند (backward compatible)

### Performance:
- تعداد API call ها ممکن است افزایش یابد (برای accuracy)
- caching strategies برای metadata
- async/await optimization

### Security:
- حفظ امنیت در metadata serialization
- logging sensitive data جلوگیری
- proper error message handling

## نتیجه‌گیری

این اصلاحات ماژول بانک گردشگری را به یک implementation کاملاً مطابق مستندات و robust تبدیل می‌کند که:

1. **همه سناریوها** را پشتیبانی می‌کند
2. **Metadata را صحیح** مدیریت می‌کند
3. **Resilient** در برابر خطاها است
4. **Maintainable** و قابل تست است
5. **Production-ready** خواهد بود

## فایل‌های نیاز به تغییر

### اصلاح کامل:
- `Dtos/Payment/PaymentOrderMetadata.cs`
- `Workflows/GardeshgariPaymentWorkflow.cs`
- `Dtos/Payment/PaymentOrderDtoExtensions.cs`

### اصلاح جزئی:
- `Services/PaymentService.cs`
- `GardeshgariPaymentProviderService.cs`
- `GardeshgariBankOptions.cs`

### فایل‌های جدید:
- `Mappers/BankStatusMapper.cs`
- `Services/ResilientPaymentService.cs`
- `Tests/PaymentWorkflowTests.cs`
- `Tests/BankIntegrationTests.cs`
