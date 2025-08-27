using Microsoft.Extensions.Logging;
using TPG.SI.SplunkLogger.Utils;
using TPG.SI.TadbirPay.Infrastructure.Abstractions.Service;
using TPG.SI.TadbirPay.Infrastructure.Dtos;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Gateway;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Payment;
using TPG.SI.TadbirPay.Infrastructure.Enums;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Payment;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Response;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Services;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Workflows;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank;

internal class GardeshgariPaymentProviderService : PaymentProviderService, IPaymentProviderService
{
    private readonly ILogger<GardeshgariPaymentProviderService> _logger;
    private readonly GardeshgariPaymentWorkflow _paymentWorkflow;
    private readonly IPaymentService _paymentService;

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

    private readonly string _appModule = "GardeshgariWithdrawalProvider";

    /// <summary>
    /// پرداخت گروهی - فقط مرحله Register
    /// </summary>
    public async Task<ProviderResponseDto<PaymentOrderDto>> BatchTransferAsync(PaymentOrderDto paymentOrderDto, GatewayInfoDto gatewayInfo)
    {
        try
        {
            _logger.LogInformationSplunk($"Start gardeshgari batch transfer registration", paymentOrderDto.OrderId, _appModule);

            // فقط مرحله Register را انجام بده
            var result = await _paymentWorkflow.RegisterPaymentAsync(paymentOrderDto, gatewayInfo);

            if (result.IsSuccess)
            {
                _logger.LogInformationSplunk(
                    $"Gardeshgari batch transfer registered successfully",
                    paymentOrderDto.OrderId,
                    _appModule,
                    new { result.Data.TrackingId }
                );
            }
            else
            {
                _logger.LogErrorSplunk(
                    $"Gardeshgari batch transfer registration failed",
                    paymentOrderDto.OrderId,
                    _appModule,
                    new { ErrorMessage = result.Message }
                );
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk($"Exception thrown on gardeshgari batch transfer", paymentOrderDto.OrderId, _appModule, ex);
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
    public async Task<ProviderResponseDto<PaymentOrderDto>> InquiryPaymentAsync(PaymentOrderDto paymentOrderDto, GatewayInfoDto gatewayInfo)
    {
        try
        {
            _logger.LogInformationSplunk($"Gardeshgari smart inquiry request", paymentOrderDto.OrderId, _appModule);

            // استعلام هوشمند که خود تصمیم می‌گیرد چه کاری انجام دهد
            var result = await _paymentWorkflow.InquiryPaymentAsync(paymentOrderDto, gatewayInfo);

            if (result.IsSuccess)
            {
                var metadata = result.Data.GetPaymentMetadata();
                _logger.LogInformationSplunk(
                    $"Gardeshgari smart inquiry completed successfully",
                    paymentOrderDto.OrderId,
                    _appModule,
                    new
                    {
                        OrderStatus = result.Data.Status,
                        metadata.IsExecutionStarted,
                        metadata.IsExecutionCompleted,
                        metadata.LastBankStatus
                    }
                );
            }
            else
            {
                _logger.LogErrorSplunk(
                    $"Gardeshgari smart inquiry failed",
                    paymentOrderDto.OrderId,
                    _appModule,
                    new { ErrorMessage = result.Message }
                );
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk($"Exception thrown on gardeshgari payment inquiry", paymentOrderDto.OrderId, _appModule, ex);
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
            _logger.LogInformationSplunk($"Gardeshgari single transaction inquiry", paymentTransaction.OrderId, _appModule);

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
                    $"Single transaction inquiry failed",
                    paymentTransaction.OrderId,
                    _appModule,
                    new { ErrorMessage = inquiryResult.Message }
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
                _logger.LogWarning($"Transaction not found in bank inquiry result for RowNumber: {paymentTransaction.RowNumber}");
                return new ProviderResponseDto<PaymentTransactionDto>
                {
                    IsSuccess = false,
                    Message = "تراکنش مورد نظر در نتیجه استعلام بانک یافت نشد",
                    Data = paymentTransaction
                };
            }

            // بروزرسانی وضعیت تراکنش
            paymentTransaction.Status = bankTransaction.MapToStandardStatus();
            paymentTransaction.ProviderMessage = bankTransaction.ErrorDescription;

            _logger.LogInformationSplunk(
                $"Single transaction inquiry completed successfully",
                paymentTransaction.OrderId,
                _appModule,
                new
                {
                    TransactionStatus = paymentTransaction.Status,
                    BankStatus = bankTransaction.Status,
                    ErrorDescription = bankTransaction.ErrorDescription
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
            _logger.LogErrorSplunk($"Exception thrown on single transaction inquiry", paymentTransaction.OrderId, _appModule, ex);
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
    public async Task<ProviderResponseDto<PaymentTransactionDto>> SingleTransferAsync(PaymentTransactionDto paymentTransactionDto, GatewayInfoDto gatewayInfo)
    {
        _logger.LogWarningSplunk("Gardeshgari bank doesn't support single transfer", paymentTransactionDto.OrderId, _appModule);

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
        return "Hello from gardeshgari withdrawal provider (Refactored Version)";
    }
}