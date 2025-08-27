using TPG.SI.TadbirPay.Infrastructure.Dtos.Gateway;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Payment;
using TPG.SI.TadbirPay.Infrastructure.Enums;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Response;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Services;

/// <summary>
/// رابط سرویس پرداخت گروهی بانک گردشگری
/// </summary>
internal interface IPaymentService
{
    #region Payment Operations
    /// <summary>
    /// ثبت پرداخت گروهی
    /// </summary>
    Task<RegisterGroupPaymentResult> RegisterGroupPaymentAsync(
        PaymentOrderDto paymentOrderDto,
        GatewayInfoDto gatewayInfo);

    /// <summary>
    /// اجرای پرداخت گروهی (DoPayment)
    /// </summary>
    Task<BaseResponse> ExecuteGroupPaymentAsync(
        string transactionId,
        GatewayInfoDto gatewayInfo);
    #endregion

    #region Inquiry Operations
    /// <summary>
    /// بررسی آمادگی اجرای پرداخت گروهی - قبل از DoPayment
    /// API: /GroupPayment/GroupPaymentInquiry
    /// </summary>
    Task<ReadinessInquiryResult> CheckExecutionReadinessAsync(
        string transactionId,
        GatewayInfoDto gatewayInfo);

    /// <summary>
    /// استعلام جزئیات تراکنش‌های پرداخت گروهی - بعد از DoPayment
    /// API: /GroupPayment/GroupPaymentInquiryFromCore
    /// </summary>
    Task<TransactionInquiryResult> InquiryTransactionDetailsAsync(
        TransactionInquiryRequest request,
        GatewayInfoDto gatewayInfo);
    #endregion


    /// <summary>
    /// تولید شناسه تراکنش
    /// </summary>
    string GenerateTransactionId(string orgCode, int randomLength = 32);

    /// <summary>
    /// تعیین نوع روش پرداخت
    /// </summary>
    PaymentMethodEnum DeterminePaymentMethod(string transactionType);

}

