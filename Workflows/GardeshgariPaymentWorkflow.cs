using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TPG.SI.SplunkLogger.Utils;
using TPG.SI.TadbirPay.Infrastructure.Dtos;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Gateway;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Payment;
using TPG.SI.TadbirPay.Infrastructure.Enums;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Payment;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Response;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Mappers;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Services;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Workflows;

/// <summary>
/// مدیریت workflow پرداخت گروهی بانک گردشگری - نسخه بازطراحی شده
/// </summary>
internal class GardeshgariPaymentWorkflow
{
    private readonly ILogger<GardeshgariPaymentWorkflow> _logger;
    private readonly IPaymentService _paymentService;
    private readonly IOptions<GardeshgariBankOptions> _options;
    private readonly string _appModule = "GardeshgariPaymentWorkflow";

    public GardeshgariPaymentWorkflow(
        ILogger<GardeshgariPaymentWorkflow> logger,
        IPaymentService paymentService,
        IOptions<GardeshgariBankOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
            _logger.LogInformationSplunk(
                "Starting group payment registration",
                paymentOrder.OrderId,
                _appModule,
                new { TransactionCount = paymentOrder.Transactions.Count }
            );

            // ثبت درخواست پرداخت گروهی در بانک
            var registerResult = await _paymentService.RegisterGroupPaymentAsync(paymentOrder, gatewayInfo);

            if (!registerResult.IsSuccess)
            {
                _logger.LogErrorSplunk(
                    "Group payment registration failed",
                    paymentOrder.OrderId,
                    _appModule,
                    new { ErrorMessage = registerResult.Message, ErrorCode = registerResult.ErrorCode }
                );

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
            var metadata = new PaymentOrderMetadata
            {
                CurrentPhase = PaymentPhase.Registered,
                LastBankStatus = "GROUP_PAYMENT_REGISTERED_STATE",
                LastInquiryTime = DateTime.UtcNow
            };
            paymentOrder.UpdatePaymentMetadata(metadata);

            response.Data = paymentOrder;
            response.Message = "ثبت تراکنش گروهی با موفقیت انجام شد";

            _logger.LogInformationSplunk(
                "Group payment registered successfully",
                paymentOrder.OrderId,
                _appModule,
                new { TrackingId = registerResult.TrackingId, Phase = metadata.CurrentPhase }
            );

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                "Exception in register payment",
                paymentOrder.OrderId,
                _appModule,
                ex
            );

            response.IsSuccess = false;
            response.HasException = true;
            response.Message = "خطا در ثبت پرداخت گروهی. لطفا بعدا تلاش کنید.";
            return response;
        }
    }

    /// <summary>
    /// استعلام هوشمند پرداخت گروهی - لاجیک کاملاً بازطراحی شده
    /// </summary>
    public async Task<ProviderResponseDto<PaymentOrderDto>> InquiryPaymentAsync(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo)
    {
        var response = new ProviderResponseDto<PaymentOrderDto> { IsSuccess = true };

        try
        {
            _logger.LogInformationSplunk(
                "Starting smart inquiry workflow",
                paymentOrder.OrderId,
                _appModule,
                new { CurrentPhase = paymentOrder.GetCurrentPhase() }
            );

            // مرحله 1: همیشه وضعیت کلی را از بانک بگیر
            var readinessResult = await CheckBankStatusAsync(paymentOrder, gatewayInfo);
            if (!readinessResult.IsSuccess)
            {
                return readinessResult;
            }

            var metadata = paymentOrder.GetPaymentMetadata();

            // مرحله 2: تصمیم‌گیری بر اساس وضعیت بانک
            var bankStatus = metadata.LastBankStatus?.ToUpper();

            _logger.LogInformationSplunk(
                "Bank status determined",
                paymentOrder.OrderId,
                _appModule,
                new { BankStatus = bankStatus, CurrentPhase = metadata.CurrentPhase }
            );

            return bankStatus switch
            {
                "READY" => await HandleReadyStateAsync(paymentOrder, gatewayInfo),
                "DONE" => await HandleCompletedStateAsync(paymentOrder, gatewayInfo),
                "GROUP_PAYMENT_ERROR_STATE" or "FAILED" or "FAIL" =>
                    HandleErrorState(paymentOrder, readinessResult.ErrorMessage ?? "خطای نامشخص بانک"),
                "CANCELED" or "CANCELLED" =>
                    HandleCanceledState(paymentOrder),
                "EXPIRED" =>
                    HandleExpiredState(paymentOrder),
                _ => HandlePendingState(paymentOrder, bankStatus)
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                "Exception in smart inquiry workflow",
                paymentOrder.OrderId,
                _appModule,
                ex
            );

            response.IsSuccess = false;
            response.HasException = true;
            response.Message = "خطا در استعلام پرداخت گروهی. لطفا بعدا تلاش کنید.";
            return response;
        }
    }

    /// <summary>
    /// بررسی وضعیت کلی از بانک و به‌روزرسانی metadata
    /// </summary>
    private async Task<ProviderResponseDto<PaymentOrderDto>> CheckBankStatusAsync(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo)
    {
        _logger.LogInformationSplunk(
            "Checking bank status",
            paymentOrder.OrderId,
            _appModule
        );

        // استعلام آمادگی از API اول
        var readinessResult = await _paymentService.CheckExecutionReadinessAsync(
            paymentOrder.TrackingId,
            gatewayInfo);

        if (!readinessResult.IsSuccess)
        {
            _logger.LogWarning(
            "Bank status check failed",
            paymentOrder.OrderId,
            _appModule,
            new { ErrorMessage = readinessResult.Message, ErrorCode = readinessResult.ErrorCode }
        );

            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = false,
                Message = $"خطا در دریافت وضعیت از بانک: {readinessResult.Message}",
                Data = paymentOrder
            };
        }

        // به‌روزرسانی metadata بر اساس وضعیت دریافت شده از بانک
        paymentOrder.UpdateFromBankStatus(readinessResult.TransactionStatus);

        // به‌روزرسانی وضعیت کلی سفارش
        paymentOrder.Status = BankStatusMapper.MapOrderStatus(readinessResult.TransactionStatus);

        _logger.LogInformationSplunk(
            "Bank status updated",
            paymentOrder.OrderId,
            _appModule,
            new
            {
                BankStatus = readinessResult.TransactionStatus,
                OrderStatus = paymentOrder.Status,
                IsReady = readinessResult.IsReadyForExecution
            }
        );

        return new ProviderResponseDto<PaymentOrderDto>
        {
            IsSuccess = true,
            Data = paymentOrder,
            Message = readinessResult.Message
        };
    }

    /// <summary>
    /// مدیریت حالت READY - اجرای DoPayment
    /// </summary>
    private async Task<ProviderResponseDto<PaymentOrderDto>> HandleReadyStateAsync(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo)
    {
        _logger.LogInformationSplunk(
            "Handling READY state",
            paymentOrder.OrderId,
            _appModule,
            new { CanRetry = paymentOrder.CanRetryExecution() }
        );

        // اگر DoPayment قبلاً اجرا شده، به استعلام تفصیلی برو
        if (paymentOrder.IsDoPaymentCompleted())
        {
            return await PerformDetailedInquiryAsync(paymentOrder, gatewayInfo);
        }

        // اگر امکان retry وجود دارد، DoPayment را اجرا کن
        if (paymentOrder.CanRetryExecution())
        {
            return await ExecutePaymentAsync(paymentOrder, gatewayInfo);
        }

        // در غیر این صورت، آخرین خطا را گزارش کن
        var metadata = paymentOrder.GetPaymentMetadata();
        return new ProviderResponseDto<PaymentOrderDto>
        {
            IsSuccess = false,
            Message = $"حداکثر تعداد تلاش برای اجرا انجام شده: {metadata.LastExecutionError}",
            Data = paymentOrder
        };
    }

    /// <summary>
    /// مدیریت حالت DONE - استعلام تفصیلی
    /// </summary>
    private async Task<ProviderResponseDto<PaymentOrderDto>> HandleCompletedStateAsync(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo)
    {
        _logger.LogInformationSplunk(
            "Handling DONE state - performing detailed inquiry",
            paymentOrder.OrderId,
            _appModule
        );

        return await PerformDetailedInquiryAsync(paymentOrder, gatewayInfo);
    }

    /// <summary>
    /// مدیریت حالت خطا
    /// </summary>
    private ProviderResponseDto<PaymentOrderDto> HandleErrorState(
        PaymentOrderDto paymentOrder,
        string errorMessage)
    {
        _logger.LogErrorSplunk(
            "Payment is in error state",
            paymentOrder.OrderId,
            _appModule,
            new { ErrorMessage = errorMessage }
        );

        paymentOrder.Status = PaymentStatusEnum.BankRejected;
        paymentOrder.MarkPhaseCompleted(PaymentPhase.Failed);

        // تنظیم وضعیت همه تراکنش‌ها روی خطا
        foreach (var transaction in paymentOrder.Transactions)
        {
            if (transaction.Status == PaymentItemStatusEnum.WaitForExecution ||
                transaction.Status == PaymentItemStatusEnum.WaitForBank)
            {
                transaction.Status = PaymentItemStatusEnum.BankRejected;
                transaction.ProviderMessage = errorMessage;
            }
        }

        return new ProviderResponseDto<PaymentOrderDto>
        {
            IsSuccess = false,
            Message = $"پرداخت توسط بانک رد شده: {errorMessage}",
            Data = paymentOrder
        };
    }

    /// <summary>
    /// مدیریت حالت لغو
    /// </summary>
    private ProviderResponseDto<PaymentOrderDto> HandleCanceledState(PaymentOrderDto paymentOrder)
    {
        _logger.LogInformationSplunk(
            "Payment is canceled",
            paymentOrder.OrderId,
            _appModule
        );

        paymentOrder.Status = PaymentStatusEnum.Canceled;
        paymentOrder.MarkPhaseCompleted(PaymentPhase.Failed);

        foreach (var transaction in paymentOrder.Transactions)
        {
            if (transaction.Status != PaymentItemStatusEnum.BankSucceeded)
            {
                transaction.Status = PaymentItemStatusEnum.Canceled;
            }
        }

        return new ProviderResponseDto<PaymentOrderDto>
        {
            IsSuccess = true,
            Message = "پرداخت لغو شده است",
            Data = paymentOrder
        };
    }

    /// <summary>
    /// مدیریت حالت منقضی
    /// </summary>
    private ProviderResponseDto<PaymentOrderDto> HandleExpiredState(PaymentOrderDto paymentOrder)
    {
        _logger.LogInformationSplunk(
            "Payment is expired",
            paymentOrder.OrderId,
            _appModule
        );

        paymentOrder.Status = PaymentStatusEnum.Expired;
        paymentOrder.MarkPhaseCompleted(PaymentPhase.Failed);

        foreach (var transaction in paymentOrder.Transactions)
        {
            if (transaction.Status != PaymentItemStatusEnum.BankSucceeded)
            {
                transaction.Status = PaymentItemStatusEnum.Expired;
            }
        }

        return new ProviderResponseDto<PaymentOrderDto>
        {
            IsSuccess = true,
            Message = "پرداخت منقضی شده است",
            Data = paymentOrder
        };
    }

    /// <summary>
    /// مدیریت حالت‌های در حال پردازش
    /// </summary>
    private ProviderResponseDto<PaymentOrderDto> HandlePendingState(
        PaymentOrderDto paymentOrder,
        string bankStatus)
    {
        _logger.LogInformationSplunk(
            "Payment is still processing",
            paymentOrder.OrderId,
            _appModule,
            new { BankStatus = bankStatus }
        );

        paymentOrder.Status = PaymentStatusEnum.SubmittedToBank;

        var statusDescription = BankStatusMapper.GetStatusDescription(bankStatus);

        return new ProviderResponseDto<PaymentOrderDto>
        {
            IsSuccess = true,
            Message = $"وضعیت فعلی: {statusDescription}",
            Data = paymentOrder
        };
    }

    /// <summary>
    /// اجرای DoPayment با retry logic
    /// </summary>
    private async Task<ProviderResponseDto<PaymentOrderDto>> ExecutePaymentAsync(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo)
    {
        _logger.LogInformationSplunk(
            "Executing DoPayment",
            paymentOrder.OrderId,
            _appModule,
            new { Attempt = paymentOrder.GetPaymentMetadata().ExecutionAttempts + 1 }
        );

        // علامت‌گذاری شروع اجرا
        paymentOrder.MarkExecutionStarted();

        try
        {
            // اجرای DoPayment
            var executeResult = await _paymentService.ExecuteGroupPaymentAsync(
                paymentOrder.TrackingId,
                gatewayInfo);

            if (!executeResult.IsSuccess)
            {
                _logger.LogErrorSplunk(
                    "DoPayment execution failed",
                    paymentOrder.OrderId,
                    _appModule,
                    new { ErrorMessage = executeResult.Message, RsCode = executeResult.RsCode }
                );

                paymentOrder.MarkExecutionFailed(executeResult.Message);

                // بررسی امکان retry
                if (paymentOrder.CanRetryExecution() && IsRetryableError(executeResult.RsCode))
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.Value.RetryDelayMilliseconds / 1000));
                    return await ExecutePaymentAsync(paymentOrder, gatewayInfo);
                }

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
                if (transaction.Status == PaymentItemStatusEnum.WaitForExecution)
                {
                    transaction.Status = PaymentItemStatusEnum.WaitForBank;
                }
            }

            _logger.LogInformationSplunk(
                "DoPayment executed successfully",
                paymentOrder.OrderId,
                _appModule
            );

            // بعد از اجرای موفق DoPayment، کمی صبر کن و استعلام تفصیلی انجام بده
            await Task.Delay(TimeSpan.FromSeconds(5));
            return await PerformDetailedInquiryAsync(paymentOrder, gatewayInfo);
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                "Exception during DoPayment execution",
                paymentOrder.OrderId,
                _appModule,
                ex
            );

            paymentOrder.MarkExecutionFailed($"خطای سیستمی: {ex.Message}");
            paymentOrder.Status = PaymentStatusEnum.SystemError;

            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = false,
                HasException = true,
                Message = "خطای سیستمی در اجرای پرداخت. لطفا بعدا تلاش کنید.",
                Data = paymentOrder
            };
        }
    }

    /// <summary>
    /// استعلام تفصیلی از تراکنش‌ها
    /// </summary>
    private async Task<ProviderResponseDto<PaymentOrderDto>> PerformDetailedInquiryAsync(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo)
    {
        _logger.LogInformationSplunk(
            "Performing detailed inquiry",
            paymentOrder.OrderId,
            _appModule
        );

        try
        {
            // ایجاد درخواست استعلام تفصیلی برای همه تراکنش‌ها
            var request = new TransactionInquiryRequest
            {
                TransactionId = paymentOrder.TrackingId,
                FirstIndex = paymentOrder.Transactions.Min(t => t.RowNumber),
                LastIndex = paymentOrder.Transactions.Max(t => t.RowNumber)
            };

            var inquiryResult = await _paymentService.InquiryTransactionDetailsAsync(request, gatewayInfo);

            if (!inquiryResult.IsSuccess)
            {
                _logger.LogWarningSplunk(
                    "Detailed inquiry failed",
                    paymentOrder.OrderId,
                    _appModule,
                    new { ErrorMessage = inquiryResult.Message }
                );

                return new ProviderResponseDto<PaymentOrderDto>
                {
                    IsSuccess = false,
                    Message = $"خطا در استعلام تفصیلی: {inquiryResult.Message}",
                    Data = paymentOrder
                };
            }

            // به‌روزرسانی وضعیت‌ها بر اساس نتیجه استعلام
            UpdateStatusesFromDetailedInquiry(paymentOrder, inquiryResult);

            // به‌روزرسانی metadata
            paymentOrder.UpdateTransactionStatuses(inquiryResult);
            paymentOrder.MarkPhaseCompleted(PaymentPhase.Completed);

            _logger.LogInformationSplunk(
                "Detailed inquiry completed successfully",
                paymentOrder.OrderId,
                _appModule,
                new
                {
                    OrderStatus = paymentOrder.Status,
                    CompletedTransactions = inquiryResult.Transactions.Count(t =>
                        BankStatusMapper.IsSuccessStatus(t.Status)),
                    FailedTransactions = inquiryResult.Transactions.Count(t =>
                        BankStatusMapper.IsErrorStatus(t.Status))
                }
            );

            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = true,
                Message = "استعلام تفصیلی با موفقیت انجام شد",
                Data = paymentOrder
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                "Exception during detailed inquiry",
                paymentOrder.OrderId,
                _appModule,
                ex
            );

            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = false,
                HasException = true,
                Message = "خطای سیستمی در استعلام تفصیلی. لطفا بعدا تلاش کنید.",
                Data = paymentOrder
            };
        }
    }

    /// <summary>
    /// به‌روزرسانی وضعیت‌ها بر اساس نتیجه استعلام تفصیلی
    /// </summary>
    private static void UpdateStatusesFromDetailedInquiry(
        PaymentOrderDto paymentOrder,
        TransactionInquiryResult inquiryResult)
    {
        // به‌روزرسانی وضعیت تراکنش‌ها
        foreach (var transaction in paymentOrder.Transactions)
        {
            var bankTransaction = inquiryResult.FindTransaction(transaction.RowNumber);
            if (bankTransaction != null)
            {
                transaction.Status = bankTransaction.MapToStandardStatus();
                transaction.ProviderMessage = bankTransaction.ErrorDescription;

                // اگر تراکنش موفق بوده، reference number را ذخیره کن
                if (transaction.Status == PaymentItemStatusEnum.BankSucceeded)
                {
                    transaction.TrackingId = bankTransaction.RefrenceNumber ?? transaction.TrackingId;
                }
            }
        }

        // تعیین وضعیت کلی بر اساس وضعیت تراکنش‌ها
        var transactionStatuses = paymentOrder.Transactions.Select(t => t.Status).ToList();
        paymentOrder.Status = BankStatusMapper.DetermineOrderStatusFromTransactions(transactionStatuses);
    }

    /// <summary>
    /// آیا خطا قابل تکرار است
    /// </summary>
    private static bool IsRetryableError(int? errorCode)
    {
        if (!errorCode.HasValue)
            return false;

        // خطاهای قابل تکرار (timeout, connection issues)
        var retryableErrors = new[] { 408, 500, 502, 503, 504 };
        return retryableErrors.Contains(errorCode.Value);
    }
}