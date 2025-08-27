using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank
{
    // <summary>
    /// ثوابت مربوط به وضعیت‌های بانک گردشگری
    /// </summary>
    internal static class GardeshgariConstants
    {
        /// <summary>
        /// وضعیت‌های آمادگی اجرای پرداخت
        /// </summary>
        public static class ReadinessStatuses
        {
            public const string Ready = "READY";
            public const string Processing = "PROCESSING";
            public const string Failed = "FAILED";
            public const string Waiting = "GROUP_PAYMENT_WAITING_STATE";
            public const string Registered = "GROUP_PAYMENT_REGISTERED_STATE";
            public const string Uploading = "GROUP_PAYMENT_UPLOADING_STATE";
            public const string Error = "GROUP_PAYMENT_ERROR_STATE";
        }

        /// <summary>
        /// وضعیت‌های تراکنش‌ها
        /// </summary>
        public static class TransactionStatuses
        {
            public const string Done = "DONE";
            public const string Failed = "FAIL";
            public const string Todo = "TODO";
            public const string InProgress = "INPROGRESS";
            public const string Canceled = "CANCELED";
            public const string Expired = "EXPIRED";
            public const string Unknown = "UNKNOWN";
        }

        /// <summary>
        /// نوع‌های تراکنش
        /// </summary>
        public static class TransactionTypes
        {
            public const string Internal = "INTERNAL";
            public const string Paya = "PAYA";
            public const string Satna = "SATNA";
            public const string Card = "CARD";
        }
    }
}
