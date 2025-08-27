using Microsoft.Extensions.Logging;
using TPG.SI.TadbirPay.Infrastructure.Dtos;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Gateway;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Payment;
using TPG.SI.TadbirPay.Infrastructure.Enums;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Payment;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Response;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Services;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Workflows;

/// <summary>
/// مدیریت workflow پرداخت گروهی بانک گردشگری
/// </summary>
internal class GardeshgariPaymentWorkflow
{
    private readonly ILogger<GardeshgariPaymentWorkflow> _logger;
    private readonly IPaymentService _paymentService;
    private readonly string _appModule = "GardeshgariPaymentWorkflow";

    public GardeshgariPaymentWorkflow(
        ILogger<GardeshgariPaymentWorkflow> logger,
        IPaymentService paymentService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
    }

    /// <summary>
    /// ثبت اولیه پرداخت گروهی
    /// </summary>
    public async Task<ProviderResponseDto<PaymentOrderDto>> RegisterPaymentAsync(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo)
    {
        var response = new ProviderResponseDto<PaymentOrderDto> { IsSuccess = true };

        try
        {
            _logger.LogInformation($"Starting group payment registration for OrderId: {paymentOrder.OrderId}");

            // ثبت درخواست پرداخت گروهی در بانک
            var registerResult = await _paymentService.RegisterGroupPaymentAsync(paymentOrder, gatewayInfo);

            if (!registerResult.IsSuccess)
            {
                response.IsSuccess = false;
                response.Message = registerResult.Message;
                return response;
            }

            // ذخیره شناسه پیگیری بانک
            paymentOrder.TrackingId = registerResult.TrackingId;

            // تنظیم وضعیت اولیه
            paymentOrder.Status = PaymentStatusEnum.SubmittedToBank;

            // تنظیم وضعیت تراکنش‌ها
            foreach (var transaction in paymentOrder.Transactions)
            {
                transaction.Status = PaymentItemStatusEnum.WaitForExecution;
                transaction.TrackingId = paymentOrder.TrackingId;
            }

            // ایجاد metadata اولیه
            var metadata = new PaymentOrderMetadata();
            paymentOrder.UpdatePaymentMetadata(metadata);

            response.Data = paymentOrder;
            response.Message = "ثبت تراکنش گروهی با موفقیت انجام شد";

            _logger.LogInformation($"Group payment registered successfully with TrackingId: {registerResult.TrackingId}");

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception in register payment for OrderId: {paymentOrder.OrderId}");
            response.IsSuccess = false;
            response.HasException = true;
            response.Message = "خطا در ثبت پرداخت گروهی. لطفا بعدا تلاش کنید.";
            return response;
        }
    }

    /// <summary>
    /// استعلام هوشمند پرداخت گروهی
    /// </summary>
    public async Task<ProviderResponseDto<PaymentOrderDto>> InquiryPaymentAsync(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo)
    {
        var response = new ProviderResponseDto<PaymentOrderDto> { IsSuccess = true };

        try
        {
            _logger.LogInformation($"Starting smart inquiry for OrderId: {paymentOrder.OrderId}");

            var metadata = paymentOrder.GetPaymentMetadata();

            // مرحله 1: اگر هنوز Execute نشده، ابتدا وضعیت آمادگی را بررسی کن
            if (metadata.NeedsExecution())
            {
                var readinessResult = await CheckExecutionReadinessAsync(paymentOrder, gatewayInfo, metadata);
                if (!readinessResult.IsSuccess)
                {
                    return readinessResult;
                }
            }

            // مرحله 2: استعلام نهایی از سرویس مناسب
            var finalInquiryResult = await PerformFinalInquiryAsync(paymentOrder, gatewayInfo, metadata);
            if (!finalInquiryResult.IsSuccess)
            {
                return finalInquiryResult;
            }

            response.Data = paymentOrder;
            response.Message = "استعلام پرداخت گروهی با موفقیت انجام شد";

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception in inquiry payment for OrderId: {paymentOrder.OrderId}");
            response.IsSuccess = false;
            response.HasException = true;
            response.Message = "خطا در استعلام پرداخت گروهی. لطفا بعدا تلاش کنید.";
            return response;
        }
    }

    /// <summary>
    /// بررسی آمادگی برای اجرا و اجرای DoPayment در صورت لزوم - بروزرسانی شده
    /// </summary>
    private async Task<ProviderResponseDto<PaymentOrderDto>> CheckExecutionReadinessAsync(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo,
        PaymentOrderMetadata metadata)
    {
        _logger.LogInformation($"Checking execution readiness for OrderId: {paymentOrder.OrderId}");

        // استعلام آمادگی از API اول
        var readinessResult = await _paymentService.CheckExecutionReadinessAsync(paymentOrder.TrackingId, gatewayInfo);

        if (!readinessResult.IsSuccess)
        {
            _logger.LogWarning($"Readiness inquiry failed for OrderId: {paymentOrder.OrderId}. Message: {readinessResult.Message}");
            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = false,
                Message = $"خطا در بررسی آمادگی: {readinessResult.Message}",
                Data = paymentOrder
            };
        }

        // به‌روزرسانی وضعیت در metadata
        metadata.UpdateInquiryStatus(readinessResult.TransactionStatus);
        paymentOrder.UpdatePaymentMetadata(metadata);

        _logger.LogInformation($"Current status for OrderId: {paymentOrder.OrderId} is: {readinessResult.TransactionStatus}");

        // اگر Ready است، DoPayment را اجرا کن
        if (readinessResult.IsReadyForExecution)
        {
            return await ExecutePaymentAsync(paymentOrder, gatewayInfo, metadata);
        }

        // اگر خطا دارد
        if (readinessResult.IsFailed)
        {
            paymentOrder.Status = PaymentStatusEnum.BankRejected;
            var errorMessages = string.Join(", ", readinessResult.RecordErrors.Select(e => e.Description));
            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = false,
                Message = $"درخواست پرداخت ناموفق: {errorMessages}",
                Data = paymentOrder
            };
        }

        // اگر هنوز Ready نیست، وضعیت کلی را بر اساس پاسخ تنظیم کن
        UpdateOrderStatusBasedOnReadiness(paymentOrder, readinessResult.TransactionStatus);

        return new ProviderResponseDto<PaymentOrderDto>
        {
            IsSuccess = true,
            Message = $"وضعیت فعلی: {GetStatusDescription(readinessResult.TransactionStatus)}",
            Data = paymentOrder
        };
    }

    /// <summary>
    /// اجرای DoPayment
    /// </summary>
    private async Task<ProviderResponseDto<PaymentOrderDto>> ExecutePaymentAsync(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo,
        PaymentOrderMetadata metadata)
    {
        _logger.LogInformation($"Executing DoPayment for OrderId: {paymentOrder.OrderId}");

        // علامت‌گذاری شروع اجرا
        paymentOrder.MarkExecutionStarted();

        // اجرای DoPayment
        var executeResult = await _paymentService.ExecuteGroupPaymentAsync(paymentOrder.TrackingId, gatewayInfo);

        if (!executeResult.IsSuccess)
        {
            _logger.LogError($"DoPayment failed for OrderId: {paymentOrder.OrderId}. Message: {executeResult.Message}");

            paymentOrder.MarkExecutionFailed(executeResult.Message);
            paymentOrder.Status = PaymentStatusEnum.BankRejected;

            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = false,
                Message = $"خطا در اجرای پرداخت: {executeResult.Message}",
                Data = paymentOrder
            };
        }

        // علامت‌گذاری تکمیل موفق اجرا
        paymentOrder.MarkExecutionCompleted();
        paymentOrder.Status = PaymentStatusEnum.SubmittedToBank;

        // تغییر وضعیت تراکنش‌ها
        foreach (var transaction in paymentOrder.Transactions)
        {
            transaction.Status = PaymentItemStatusEnum.WaitForBank;
        }

        _logger.LogInformation($"DoPayment executed successfully for OrderId: {paymentOrder.OrderId}");

        return new ProviderResponseDto<PaymentOrderDto>
        {
            IsSuccess = true,
            Message = "اجرای پرداخت با موفقیت انجام شد",
            Data = paymentOrder
        };
    }

    /// <summary>
    /// استعلام نهایی از سرویس مناسب
    /// </summary>
    private async Task<ProviderResponseDto<PaymentOrderDto>> PerformFinalInquiryAsync(
       PaymentOrderDto paymentOrder,
       GatewayInfoDto gatewayInfo,
       PaymentOrderMetadata metadata)
    {
        _logger.LogInformation($"Performing final inquiry for OrderId: {paymentOrder.OrderId}");

        // ایجاد درخواست استعلام تفصیلی
        var request = new TransactionInquiryRequest
        {
            TransactionId = paymentOrder.TrackingId,
            FirstIndex = paymentOrder.Transactions.Min(t => t.RowNumber),
            LastIndex = paymentOrder.Transactions.Max(t => t.RowNumber)
        };

        _logger.LogInformation($"Using TransactionInquiry API for OrderId: {paymentOrder.OrderId}");

        var inquiryResult = await _paymentService.InquiryTransactionDetailsAsync(request, gatewayInfo);

        if (!inquiryResult.IsSuccess)
        {
            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = false,
                Message = inquiryResult.Message,
                Data = paymentOrder
            };
        }

        // به‌روزرسانی وضعیت‌ها بر اساس نتیجه استعلام
        UpdateStatusesFromDetailedInquiry(paymentOrder, inquiryResult);

        return new ProviderResponseDto<PaymentOrderDto>
        {
            IsSuccess = true,
            Message = "استعلام با موفقیت انجام شد",
            Data = paymentOrder
        };
    }

    /// <summary>
    /// آیا آماده اجرا است
    /// </summary>
    private static bool IsReadyForExecution(string status)
    {
        return status?.ToUpper() == "READY";
    }

    /// <summary>
    /// به‌روزرسانی وضعیت سفارش بر اساس آمادگی
    /// </summary>
    private static void UpdateOrderStatusBasedOnReadiness(PaymentOrderDto paymentOrder, string status)
    {
        paymentOrder.Status = status?.ToUpper() switch
        {
            "PROCESSING" => PaymentStatusEnum.SubmittedToBank,
            "FAILED" => PaymentStatusEnum.BankRejected,
            "GROUP_PAYMENT_ERROR_STATE" => PaymentStatusEnum.BankRejected,
            _ => PaymentStatusEnum.SubmittedToBank
        };
    }

    /// <summary>
    /// به‌روزرسانی وضعیت‌ها بر اساس نتیجه استعلام
    /// </summary>
    private static void UpdateStatusesFromInquiry(PaymentOrderDto paymentOrder, InquiryResult inquiryResult)
    {
        // به‌روزرسانی وضعیت کلی سفارش
        paymentOrder.Status = inquiryResult.MapOrderStatus();
        paymentOrder.UpdateInquiryStatus(inquiryResult.Status);

        // به‌روزرسانی وضعیت تراکنش‌ها
        if (inquiryResult.Transactions != null)
        {
            foreach (var transaction in paymentOrder.Transactions)
            {
                var bankTransaction = inquiryResult.FindTransaction(transaction.RowNumber);
                if (bankTransaction != null)
                {
                    transaction.Status = inquiryResult.MapTransactionStatus(bankTransaction.Status);
                    transaction.ProviderMessage = bankTransaction.ErrorDescription;
                }
            }
        }
    }


    /// <summary>
    /// به‌روزرسانی وضعیت‌ها بر اساس نتیجه استعلام تفصیلی
    /// </summary>
    private static void UpdateStatusesFromDetailedInquiry(PaymentOrderDto paymentOrder, TransactionInquiryResult inquiryResult)
    {
        // به‌روزرسانی وضعیت تراکنش‌ها
        foreach (var transaction in paymentOrder.Transactions)
        {
            var bankTransaction = inquiryResult.FindTransaction(transaction.RowNumber);
            if (bankTransaction != null)
            {
                transaction.Status = bankTransaction.MapToStandardStatus();
                transaction.ProviderMessage = bankTransaction.ErrorDescription;
            }
        }

        // تعیین وضعیت کلی بر اساس وضعیت تراکنش‌ها
        var transactionStatuses = paymentOrder.Transactions.Select(t => t.Status).Distinct().ToList();

        if (transactionStatuses.All(s => s == PaymentItemStatusEnum.BankSucceeded))
        {
            paymentOrder.Status = PaymentStatusEnum.BankSucceeded;
        }
        else if (transactionStatuses.Any(s => s == PaymentItemStatusEnum.BankSucceeded))
        {
            paymentOrder.Status = PaymentStatusEnum.DoneWithError;
        }
        else if (transactionStatuses.All(s => s == PaymentItemStatusEnum.BankRejected))
        {
            paymentOrder.Status = PaymentStatusEnum.BankRejected;
        }
        else
        {
            paymentOrder.Status = PaymentStatusEnum.SubmittedToBank;
        }
    }

    /// <summary>
    /// توضیح وضعیت
    /// </summary>
    private static string GetStatusDescription(string status)
    {
        return status?.ToUpper() switch
        {
            "PROCESSING" => "در حال پردازش",
            "READY" => "آماده اجرا",
            "FAILED" => "ناموفق",
            "GROUP_PAYMENT_WAITING_STATE" => "در انتظار",
            "GROUP_PAYMENT_REGISTERED_STATE" => "ثبت شده",
            "GROUP_PAYMENT_UPLOADING_STATE" => "در حال آپلود",
            "GROUP_PAYMENT_ERROR_STATE" => "خطا در پردازش",
            _ => status ?? "نامشخص"
        };
    }
}


