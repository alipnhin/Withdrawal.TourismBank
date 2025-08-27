using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TPG.SI.TadbirPay.Infrastructure.Enums;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Response;

// <summary>
/// نتیجه بررسی آمادگی اجرای پرداخت
/// </summary>
internal class ReadinessInquiryResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
    public string ErrorCode { get; set; }

    /// <summary>
    /// وضعیت آمادگی: Ready, Processing, Failed
    /// </summary>
    public string TransactionStatus { get; set; }

    /// <summary>
    /// اطلاعات کلی پرداخت گروهی
    /// </summary>
    public PaymentSummaryInfo PaymentSummary { get; set; }

    /// <summary>
    /// لیست خطاهای ثبت رکوردها (اگر وجود داشته باشد)
    /// </summary>
    public List<RecordError> RecordErrors { get; set; } = new List<RecordError>();

    /// <summary>
    /// آیا آماده اجرا است
    /// </summary>
    public bool IsReadyForExecution => TransactionStatus?.ToUpper() == "READY";

    /// <summary>
    /// آیا ناموفق است
    /// </summary>
    public bool IsFailed => TransactionStatus?.ToUpper() == "FAILED";
}

/// <summary>
/// اطلاعات خلاصه پرداخت گروهی
/// </summary>
internal class PaymentSummaryInfo
{
    public string TransactionId { get; set; }
    public string CustomerNumber { get; set; }
    public string State { get; set; }
    public string DateTime { get; set; }
    public string LineCount { get; set; }
    public string TotalAmount { get; set; }
    public string TotalInternalAmount { get; set; }
    public string TotalPayaAmount { get; set; }
    public string TotalSatnaAmount { get; set; }
    public string SourceDeposit { get; set; }
    public string RefundDeposit { get; set; }
    public string SourceDepositCommission { get; set; }
}

/// <summary>
/// خطای رکورد در مرحله ثبت
/// </summary>
internal class RecordError
{
    public string Code { get; set; }
    public string Description { get; set; }
    public string ParamName { get; set; }
    public string ParamPath { get; set; }
}

/// <summary>
/// پاسخ API GroupPaymentInquiry
/// </summary>
internal class ReadinessInquiryResponse : BaseResponse
{
    public ReadinessInquiryData ResultData { get; set; }
}

internal class ReadinessInquiryData
{
    public string TransactionStatus { get; set; }
    public List<RecordErrorResponse> RecordErrorsList { get; set; }
    public PaymentSummaryResponse Result { get; set; }
}

internal class RecordErrorResponse
{
    public string Code { get; set; }
    public string Desc { get; set; }
    public string ParamName { get; set; }
    public string ParamPath { get; set; }
}

internal class PaymentSummaryResponse
{
    public string TransactionId { get; set; }
    public string DateTime { get; set; }
    public string CustomerNumber { get; set; }
    public string State { get; set; }
    public string LineCount { get; set; }
    public string TotalAmount { get; set; }
    public string TotalInternalAmount { get; set; }
    public string TotalPayaAmount { get; set; }
    public string TotalSatnaAmount { get; set; }
    public string TotalCommissionPayaAmount { get; set; }
    public string TotalCommissionSatnaAmount { get; set; }
    public string TotalCommissionInternalAmount { get; set; }
    public string CountPaya { get; set; }
    public string InternalRecordCount { get; set; }
    public string SourceDeposit { get; set; }
    public string RefundDeposit { get; set; }
    public string SourceDepositCommission { get; set; }
    public object ThresholdAmount { get; set; }
}

// =====================================================
// 3. مدل‌های جدید برای API دوم (GroupPaymentInquiryFromCore)
// =====================================================

/// <summary>
/// درخواست استعلام جزئیات تراکنش‌ها
/// </summary>
internal class TransactionInquiryRequest
{
    public string TransactionId { get; set; }

    /// <summary>
    /// برای استعلام یک تراکنش خاص
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// برای استعلام محدوده‌ای از تراکنش‌ها - شروع
    /// </summary>
    public int? FirstIndex { get; set; }

    /// <summary>
    /// برای استعلام محدوده‌ای از تراکنش‌ها - پایان
    /// </summary>
    public int? LastIndex { get; set; }
}

/// <summary>
/// نتیجه استعلام جزئیات تراکنش‌ها
/// </summary>
internal class TransactionInquiryResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
    public string ErrorCode { get; set; }

    /// <summary>
    /// لیست تراکنش‌های مورد استعلام
    /// </summary>
    public List<TransactionDetailResult> Transactions { get; set; } = new List<TransactionDetailResult>();

    /// <summary>
    /// پیدا کردن تراکنش بر اساس شماره ردیف
    /// </summary>
    public TransactionDetailResult FindTransaction(int lineNumber)
    {
        return Transactions?.FirstOrDefault(t => t.LineNumber == lineNumber);
    }
}

/// <summary>
/// جزئیات یک تراکنش
/// </summary>
internal class TransactionDetailResult
{
    public int LineNumber { get; set; }
    public long Amount { get; set; }
    public string FinalState { get; set; }
    public string FinalMessage { get; set; }
    public string Status { get; set; }
    public string TransactionType { get; set; }
    public PaymentMethodEnum PaymentMethod { get; set; }
    public string TransactionDate { get; set; }
    public string TransactionDescription { get; set; }
    public string RefrenceNumber { get; set; }
    public string BranchCode { get; set; }
    public string DocumentNumber { get; set; }
    public string DestinationBankCode { get; set; }
    public string DestinationBankName { get; set; }
    public long TransactionCommission { get; set; }
    public string ErrorDescription { get; set; }

    /// <summary>
    /// تبدیل وضعیت بانک به enum استاندارد
    /// </summary>
    public PaymentItemStatusEnum MapToStandardStatus()
    {
        if (string.IsNullOrEmpty(Status))
            return PaymentItemStatusEnum.WaitForBank;

        return Status.ToUpper() switch
        {
            "DONE" => PaymentItemStatusEnum.BankSucceeded,
            "FAIL" or "FAILED" => PaymentItemStatusEnum.BankRejected,
            "TODO" => PaymentItemStatusEnum.WaitForExecution,
            "INPROGRESS" => PaymentItemStatusEnum.WaitForBank,
            "CANCELED" => PaymentItemStatusEnum.Canceled,
            "EXPIRED" => PaymentItemStatusEnum.Expired,
            "UNKNOWN" => PaymentItemStatusEnum.WaitForBank,
            _ => PaymentItemStatusEnum.WaitForBank
        };
    }
}

/// <summary>
/// پاسخ API GroupPaymentInquiryFromCore
/// </summary>
internal class TransactionInquiryResponse : BaseResponse
{
    public TransactionInquiryData ResultData { get; set; }
}

internal class TransactionInquiryData
{
    /// <summary>
    /// لیست تراکنش‌ها - برای استعلام لیستی
    /// </summary>
    public List<TransactionDetailResponse> Result { get; set; }

    /// <summary>
    /// تراکنش تکی - برای استعلام با LineNumber
    /// </summary>
    public TransactionDetailResponse SingleResult { get; set; }
}

internal class TransactionDetailResponse
{
    public long Amount { get; set; }
    public string FinalState { get; set; }
    public string FinalMessage { get; set; }
    public string Status { get; set; }
    public string TransactionType { get; set; }
    public string TransactionDate { get; set; }
    public string TransactionDescription { get; set; }
    public string RefrenceNumber { get; set; }
    public string BranchCode { get; set; }
    public string DocumentNumber { get; set; }
    public string DestinationBankCode { get; set; }
    public string DestinationBankName { get; set; }
    public long TransactionCommission { get; set; }
    public string ErrorDescription { get; set; }
    public string LineNumber { get; set; }
}

