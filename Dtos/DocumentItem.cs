using TPG.SI.TadbirPay.Infrastructure.Helper;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;

/// <summary>
/// مدل آیتم تراکنش برای ارسال به بانک گردشگری
/// </summary>
internal class DocumentItem
{
    /// <summary>
    /// شماره ردیف
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// مبلغ تراکنش
    /// </summary>
    public string Amount { get; set; }

    /// <summary>
    /// شماره شبای مقصد
    /// </summary>
    public string DestinationIban { get; set; }

    /// <summary>
    /// تاریخ تراکنش (فرمت عددی: yyyyMMdd)
    /// </summary>
    public int TransactionDate { get; set; }

    /// <summary>
    /// نام و نام خانوادگی گیرنده
    /// </summary>
    public string RecieverFullName { get; set; }

    /// <summary>
    /// شرح تراکنش
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// شماره قبض تراکنش (اختیاری)
    /// </summary>
    public string TransactionBillNumber { get; set; }

    /// <summary>
    /// نوع علت پرداخت (بر اساس جدول بانک)
    /// </summary>
    public int CauseType { get; set; }

    /// <summary>
    /// سپرده بازگشت وجه (در صورت خطا)
    /// </summary>
    public string RefundDeposit { get; set; }
}

/// <summary>
/// Extension methods برای DocumentItem
/// </summary>
internal static class DocumentItemExtensions
{
    /// <summary>
    /// تبدیل مبلغ به رشته
    /// </summary>
    public static string GetAmountAsString(this DocumentItem item)
    {
        return item.Amount.ToString();
    }

    /// <summary>
    /// اعتبارسنجی آیتم
    /// </summary>
    public static bool IsValid(this DocumentItem item)
    {
        return item.LineNumber > 0 &&
               item.Amount.ParseToLong() > 0 &&
               !string.IsNullOrEmpty(item.DestinationIban) &&
               item.DestinationIban.Length == 26 &&
               !string.IsNullOrEmpty(item.RecieverFullName) &&
               item.TransactionDate > 0;
    }

    /// <summary>
    /// دریافت خطاهای اعتبارسنجی
    /// </summary>
    public static List<string> GetValidationErrors(this DocumentItem item)
    {
        var errors = new List<string>();

        if (item.LineNumber <= 0)
            errors.Add("شماره ردیف باید بزرگتر از صفر باشد");

        if (item.Amount.ParseToLong() <= 0)
            errors.Add("مبلغ باید بزرگتر از صفر باشد");

        if (string.IsNullOrEmpty(item.DestinationIban))
            errors.Add("شماره شبا الزامی است");
        else if (item.DestinationIban.Length != 26)
            errors.Add("طول شماره شبا باید 26 کاراکتر باشد");

        if (string.IsNullOrEmpty(item.RecieverFullName))
            errors.Add("نام گیرنده الزامی است");

        if (item.TransactionDate <= 0)
            errors.Add("تاریخ تراکنش نامعتبر است");

        return errors;
    }
}