using Microsoft.Extensions.Logging;
using TPG.SI.SplunkLogger.Utils;
using TPG.SI.TadbirPay.Infrastructure.Abstractions.Service;
using TPG.SI.TadbirPay.Infrastructure.Dtos;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Gateway;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Payment;
using TPG.SI.TadbirPay.Infrastructure.Enums;
using TPG.SI.TadbirPay.Infrastructure.Helper;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Payment;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Response;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Services;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Workflows;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank;

/// <summary>
/// سرویس ارائه‌دهنده پرداخت بانک گردشگری - نسخه بازطراحی شده
/// </summary>
internal class GardeshgariPaymentProviderService : PaymentProviderService, IPaymentProviderService
{
    private readonly ILogger<GardeshgariPaymentProviderService> _logger;
    private readonly GardeshgariPaymentWorkflow _paymentWorkflow;
    private readonly IPaymentService _paymentService;
    private readonly string _appModule = "GardeshgariWithdrawalProvider";

    public GardeshgariPaymentProviderService(
        ILogger<GardeshgariPaymentProviderService> logger,
        GardeshgariPaymentWorkflow paymentWorkflow,
        IPaymentService paymentService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _paymentWorkflow = paymentWorkflow ?? throw new ArgumentNullException(nameof(paymentWorkflow));
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
    }

    public BankEnum ProviderKey => BankEnum.Gardeshgari;

    public bool HasBatchTransfer => true;

    /// <summary>
    /// پرداخت گروهی - مرحله Register
    /// </summary>
    public async Task<ProviderResponseDto<PaymentOrderDto>> BatchTransferAsync(
        PaymentOrderDto paymentOrderDto,
        GatewayInfoDto gatewayInfo)
    {
        try
        {
            _logger.LogInformationSplunk(
                "Starting gardeshgari batch transfer registration",
                paymentOrderDto.OrderId,
                _appModule,
                new
                {
                    TransactionCount = paymentOrderDto.Transactions.Count,
                    TotalAmount = paymentOrderDto.Transactions.Sum(t => t.Amount.ParseToLong()),
                    CurrentPhase = paymentOrderDto.GetCurrentPhase()
                }
            );

            // اعتبارسنجی اولیه
            var validationResult = ValidatePaymentOrder(paymentOrderDto, gatewayInfo);
            if (!validationResult.IsSuccess)
            {
                _logger.LogErrorSplunk(
                    "Payment order validation failed",
                    paymentOrderDto.OrderId,
                    _appModule,
                    new { ValidationErrors = validationResult.Message }
                );
                return validationResult;
            }

            // فقط مرحله Register را انجام بده
            var result = await _paymentWorkflow.RegisterPaymentAsync(paymentOrderDto, gatewayInfo);

            if (result.IsSuccess)
            {
                _logger.LogInformationSplunk(
                    "Gardeshgari batch transfer registered successfully",
                    paymentOrderDto.OrderId,
                    _appModule,
                    new
                    {
                        TrackingId = result.Data.TrackingId,
                        OrderStatus = result.Data.Status,
                        CurrentPhase = result.Data.GetCurrentPhase()
                    }
                );
            }
            else
            {
                _logger.LogErrorSplunk(
                    "Gardeshgari batch transfer registration failed",
                    paymentOrderDto.OrderId,
                    _appModule,
                    new { ErrorMessage = result.Message, HasException = result.HasException }
                );
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                "Exception thrown on gardeshgari batch transfer",
                paymentOrderDto.OrderId,
                _appModule,
                ex
            );

            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = false,
                HasException = true,
                Message = "خطا در ثبت پرداخت گروهی. لطفا بعدا تلاش کنید.",
                Data = paymentOrderDto
            };
        }
    }

    /// <summary>
    /// استعلام پرداخت گروهی - استعلام هوشمند
    /// </summary>
    public async Task<ProviderResponseDto<PaymentOrderDto>> InquiryPaymentAsync(
        PaymentOrderDto paymentOrderDto,
        GatewayInfoDto gatewayInfo)
    {
        try
        {
            _logger.LogInformationSplunk(
                "Starting gardeshgari smart inquiry",
                paymentOrderDto.OrderId,
                _appModule,
                new
                {
                    CurrentStatus = paymentOrderDto.Status,
                    CurrentPhase = paymentOrderDto.GetCurrentPhase(),
                    TrackingId = paymentOrderDto.TrackingId,
                    IsInFinalState = paymentOrderDto.IsInFinalState(),
                    LastInquiryTime = paymentOrderDto.GetLastInquiryTime()
                }
            );

            // اگر در حالت نهایی است و اخیراً استعلام شده، نیازی به استعلام مجدد نیست
            if (paymentOrderDto.IsInFinalState() &&
                !paymentOrderDto.NeedsInquiryRefresh(TimeSpan.FromMinutes(5)))
            {
                _logger.LogInformationSplunk(
                    "Payment is in final state and recently inquired, skipping inquiry",
                    paymentOrderDto.OrderId,
                    _appModule
                );

                return new ProviderResponseDto<PaymentOrderDto>
                {
                    IsSuccess = true,
                    Message = paymentOrderDto.GetCurrentStatusDescription(),
                    Data = paymentOrderDto
                };
            }

            // اعتبارسنجی TrackingId
            if (string.IsNullOrEmpty(paymentOrderDto.TrackingId))
            {
                _logger.LogErrorSplunk(
                    "TrackingId is missing for inquiry",
                    paymentOrderDto.OrderId,
                    _appModule
                );

                return new ProviderResponseDto<PaymentOrderDto>
                {
                    IsSuccess = false,
                    Message = "شناسه پیگیری تراکنش موجود نیست. ابتدا تراکنش را ثبت کنید.",
                    Data = paymentOrderDto
                };
            }

            // استعلام هوشمند که خود تصمیم می‌گیرد چه کاری انجام دهد
            var result = await _paymentWorkflow.InquiryPaymentAsync(paymentOrderDto, gatewayInfo);

            if (result.IsSuccess)
            {
                var metadata = result.Data.GetPaymentMetadata();
                _logger.LogInformationSplunk(
                    "Gardeshgari smart inquiry completed successfully",
                    paymentOrderDto.OrderId,
                    _appModule,
                    new
                    {
                        OrderStatus = result.Data.Status,
                        CurrentPhase = result.Data.GetCurrentPhase(),
                        IsExecutionCompleted = metadata.IsDoPaymentCompleted,
                        LastBankStatus = metadata.LastBankStatus,
                        TransactionStatusSummary = GetTransactionStatusSummary(result.Data)
                    }
                );
            }
            else
            {
                _logger.LogErrorSplunk(
                    "Gardeshgari smart inquiry failed",
                    paymentOrderDto.OrderId,
                    _appModule,
                    new
                    {
                        ErrorMessage = result.Message,
                        HasException = result.HasException,
                        CurrentPhase = paymentOrderDto.GetCurrentPhase()
                    }
                );
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                "Exception thrown on gardeshgari payment inquiry",
                paymentOrderDto.OrderId,
                _appModule,
                ex
            );

            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = false,
                HasException = true,
                Message = "خطا در استعلام پرداخت گروهی. لطفا بعدا تلاش کنید.",
                Data = paymentOrderDto
            };
        }
    }

    /// <summary>
    /// استعلام تراکنش تکی
    /// </summary>
    public async Task<ProviderResponseDto<PaymentTransactionDto>> InquiryTransactionAsync(
        PaymentTransactionDto paymentTransaction,
        GatewayInfoDto gatewayInfo)
    {
        try
        {
            _logger.LogInformationSplunk(
                "Starting gardeshgari single transaction inquiry",
                paymentTransaction.OrderId,
                _appModule,
                new
                {
                    TransactionId = paymentTransaction.Id,
                    RowNumber = paymentTransaction.RowNumber,
                    TrackingId = paymentTransaction.TrackingId,
                    CurrentStatus = paymentTransaction.Status
                }
            );

            // اعتبارسنجی TrackingId
            if (string.IsNullOrEmpty(paymentTransaction.TrackingId))
            {
                _logger.LogErrorSplunk(
                    "TrackingId is missing for transaction inquiry",
                    paymentTransaction.OrderId,
                    _appModule,
                    new { TransactionId = paymentTransaction.Id }
                );

                return new ProviderResponseDto<PaymentTransactionDto>
                {
                    IsSuccess = false,
                    Message = "شناسه پیگیری تراکنش موجود نیست",
                    Data = paymentTransaction
                };
            }

            // استعلام تراکنش خاص از API دوم
            var request = new TransactionInquiryRequest
            {
                TransactionId = paymentTransaction.TrackingId,
                LineNumber = paymentTransaction.RowNumber
            };

            var inquiryResult = await _paymentService.InquiryTransactionDetailsAsync(request, gatewayInfo);

            if (!inquiryResult.IsSuccess)
            {
                _logger.LogErrorSplunk(
                    "Single transaction inquiry failed",
                    paymentTransaction.OrderId,
                    _appModule,
                    new
                    {
                        ErrorMessage = inquiryResult.Message,
                        ErrorCode = inquiryResult.ErrorCode,
                        TransactionId = paymentTransaction.Id
                    }
                );

                return new ProviderResponseDto<PaymentTransactionDto>
                {
                    IsSuccess = false,
                    Message = inquiryResult.Message,
                    Data = paymentTransaction
                };
            }

            // پیدا کردن تراکنش مورد نظر
            var bankTransaction = inquiryResult.FindTransaction(paymentTransaction.RowNumber);

            if (bankTransaction == null)
            {
                _logger.LogWarningSplunk(
                    "Transaction not found in bank inquiry result",
                    paymentTransaction.OrderId,
                    _appModule,
                    new { RowNumber = paymentTransaction.RowNumber, TransactionId = paymentTransaction.Id }
                );

                return new ProviderResponseDto<PaymentTransactionDto>
                {
                    IsSuccess = false,
                    Message = "تراکنش مورد نظر در نتیجه استعلام بانک یافت نشد",
                    Data = paymentTransaction
                };
            }

            // بروزرسانی وضعیت تراکنش
            var previousStatus = paymentTransaction.Status;
            paymentTransaction.Status = bankTransaction.MapToStandardStatus();
            paymentTransaction.ProviderMessage = bankTransaction.ErrorDescription;

            // اگر تراکنش موفق بوده، reference number را بروزرسانی کن
            if (paymentTransaction.Status == PaymentItemStatusEnum.BankSucceeded &&
                !string.IsNullOrEmpty(bankTransaction.RefrenceNumber))
            {
                paymentTransaction.TrackingId = bankTransaction.RefrenceNumber;
            }

            _logger.LogInformationSplunk(
                "Single transaction inquiry completed successfully",
                paymentTransaction.OrderId,
                _appModule,
                new
                {
                    TransactionId = paymentTransaction.Id,
                    PreviousStatus = previousStatus,
                    NewStatus = paymentTransaction.Status,
                    BankStatus = bankTransaction.Status,
                    ErrorDescription = bankTransaction.ErrorDescription,
                    RefNumber = bankTransaction.RefrenceNumber
                }
            );

            return new ProviderResponseDto<PaymentTransactionDto>
            {
                IsSuccess = true,
                Message = "استعلام تراکنش با موفقیت انجام شد",
                Data = paymentTransaction
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                "Exception thrown on single transaction inquiry",
                paymentTransaction.OrderId,
                _appModule,
                ex
            );

            return new ProviderResponseDto<PaymentTransactionDto>
            {
                IsSuccess = false,
                HasException = true,
                Message = "خطا در استعلام تراکنش. لطفا بعدا تلاش کنید.",
                Data = paymentTransaction
            };
        }
    }

    /// <summary>
    /// پرداخت تکی - بانک گردشگری پشتیبانی نمی‌کند
    /// </summary>
    public async Task<ProviderResponseDto<PaymentTransactionDto>> SingleTransferAsync(
        PaymentTransactionDto paymentTransactionDto,
        GatewayInfoDto gatewayInfo)
    {
        _logger.LogWarningSplunk(
            "Gardeshgari bank doesn't support single transfer",
            paymentTransactionDto.OrderId,
            _appModule,
            new { TransactionId = paymentTransactionDto.Id }
        );

        await Task.CompletedTask; // برای حذف warning

        return new ProviderResponseDto<PaymentTransactionDto>
        {
            IsSuccess = false,
            Message = "بانک گردشگری از پرداخت تکی پشتیبانی نمی‌کند. لطفا از پرداخت گروهی استفاده کنید.",
            Data = paymentTransactionDto
        };
    }

    /// <summary>
    /// تست سرویس
    /// </summary>
    public string SayHello()
    {
        _logger.LogInformationSplunk(
            "SayHello method called",
            _appModule
        );

        return "Hello from Gardeshgari Withdrawal Provider (Refactored Version) - Smart Inquiry System";
    }

    #region Private Helper Methods

    /// <summary>
    /// اعتبارسنجی سفارش پرداخت
    /// </summary>
    private ProviderResponseDto<PaymentOrderDto> ValidatePaymentOrder(
        PaymentOrderDto paymentOrder,
        GatewayInfoDto gatewayInfo)
    {
        var errors = new List<string>();

        // بررسی اطلاعات پایه
        if (paymentOrder == null)
        {
            errors.Add("سفارش پرداخت نمی‌تواند خالی باشد");
        }
        else
        {
            if (string.IsNullOrEmpty( paymentOrder.OrderId))
                errors.Add("شناسه سفارش نامعتبر است");

            if (!paymentOrder.Transactions?.Any() ?? true)
                errors.Add("لیست تراکنش‌ها خالی است");
            else
            {
                // بررسی تراکنش‌ها
                foreach (var transaction in paymentOrder.Transactions)
                {
                    if (transaction.Amount.ParseToLong() <= 0)
                        errors.Add($"مبلغ تراکنش ردیف {transaction.RowNumber} نامعتبر است");

                    if (string.IsNullOrEmpty(transaction.DestinationIban) ||
                        transaction.DestinationIban.Length != 26)
                        errors.Add($"شبای مقصد تراکنش ردیف {transaction.RowNumber} نامعتبر است");

                    if (string.IsNullOrEmpty(transaction.DestinationAccountOwner))
                        errors.Add($"نام صاحب حساب مقصد تراکنش ردیف {transaction.RowNumber} خالی است");
                }

                // بررسی تکراری نبودن شماره ردیف‌ها
                var duplicateRows = paymentOrder.Transactions
                    .GroupBy(t => t.RowNumber)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                if (duplicateRows.Any())
                    errors.Add($"شماره ردیف‌های تکراری: {string.Join(", ", duplicateRows)}");
            }
        }

        // بررسی اطلاعات درگاه
        if (gatewayInfo == null)
        {
            errors.Add("اطلاعات درگاه پرداخت نمی‌تواند خالی باشد");
        }
        else
        {
            if (string.IsNullOrEmpty(gatewayInfo.AccountNumber))
                errors.Add("شماره حساب مبدا خالی است");

            if (string.IsNullOrEmpty(gatewayInfo.PrivateEncryptionKey))
                errors.Add("کلید خصوصی امضا موجود نیست");

            if (string.IsNullOrEmpty(gatewayInfo.MetaData))
                errors.Add("metadata درگاه موجود نیست");
        }

        if (errors.Any())
        {
            return new ProviderResponseDto<PaymentOrderDto>
            {
                IsSuccess = false,
                Message = string.Join("; ", errors),
                Data = paymentOrder
            };
        }

        return new ProviderResponseDto<PaymentOrderDto> { IsSuccess = true };
    }

    /// <summary>
    /// خلاصه وضعیت تراکنش‌ها
    /// </summary>
    private object GetTransactionStatusSummary(PaymentOrderDto paymentOrder)
    {
        if (!paymentOrder.Transactions?.Any() ?? true)
        {
            return new { Message = "بدون تراکنش" };
        }

        var statusGroups = paymentOrder.Transactions
            .GroupBy(t => t.Status)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        return new
        {
            TotalTransactions = paymentOrder.Transactions.Count,
            StatusBreakdown = statusGroups,
            SuccessfulTransactions = paymentOrder.Transactions.Count(t =>
                t.Status == PaymentItemStatusEnum.BankSucceeded),
            FailedTransactions = paymentOrder.Transactions.Count(t =>
                t.Status == PaymentItemStatusEnum.BankRejected),
            PendingTransactions = paymentOrder.Transactions.Count(t =>
                t.Status == PaymentItemStatusEnum.WaitForBank ||
                t.Status == PaymentItemStatusEnum.WaitForExecution)
        };
    }

    #endregion
}