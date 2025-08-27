using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Response;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Validation
{
    internal static class ValidationExtensions
    {
        /// <summary>
        /// اعتبارسنجی درخواست استعلام آمادگی
        /// </summary>
        public static bool IsValidForReadinessCheck(this string transactionId)
        {
            return !string.IsNullOrEmpty(transactionId) && transactionId.Length > 10;
        }

        /// <summary>
        /// اعتبارسنجی درخواست استعلام تراکنش
        /// </summary>
        public static bool IsValid(this TransactionInquiryRequest request)
        {
            if (string.IsNullOrEmpty(request.TransactionId))
                return false;

            // یا باید LineNumber داشته باشد یا FirstIndex/LastIndex
            var hasLineNumber = request.LineNumber.HasValue;
            var hasRange = request.FirstIndex.HasValue && request.LastIndex.HasValue;

            return hasLineNumber || hasRange;
        }

        /// <summary>
        /// دریافت خطاهای اعتبارسنجی
        /// </summary>
        public static List<string> GetValidationErrors(this TransactionInquiryRequest request)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(request.TransactionId))
                errors.Add("شناسه تراکنش الزامی است");

            var hasLineNumber = request.LineNumber.HasValue;
            var hasRange = request.FirstIndex.HasValue && request.LastIndex.HasValue;

            if (!hasLineNumber && !hasRange)
                errors.Add("باید LineNumber یا FirstIndex/LastIndex مشخص شود");

            if (hasRange && request.FirstIndex > request.LastIndex)
                errors.Add("FirstIndex نمی‌تواند بزرگتر از LastIndex باشد");

            return errors;
        }
    }
}
