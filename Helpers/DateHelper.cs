using System.Globalization;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Helpers;

internal static class DateHelper
{
    public static string GetPersianTransactionDate(DateTime dateTime)
    {
        var persianCalendar = new System.Globalization.PersianCalendar();
        var year = persianCalendar.GetYear(dateTime);
        var month = persianCalendar.GetMonth(dateTime);
        var day = persianCalendar.GetDayOfMonth(dateTime);

        return $"{year:0000}{month:00}{day:00}";
    }
}

