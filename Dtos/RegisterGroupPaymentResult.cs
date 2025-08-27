namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;

internal class RegisterGroupPaymentResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
    public string TrackingId { get; set; }
    public string ErrorCode { get; set; }
    public List<TransactionRegistrationError> ErrorList { get; set; } = new List<TransactionRegistrationError>();
    public bool HasTransactionErrors => ErrorList?.Any() == true;
    public int ErrorCount => ErrorList?.Count ?? 0;
}

internal class TransactionRegistrationError
{
    public int Code { get; set; }
    public string Desc { get; set; }
    public string ParamName { get; set; }
    public string ParamPath { get; set; }

    public int? ExtractLineNumber()
    {
        if (string.IsNullOrEmpty(ParamPath))
            return null;

        var pattern = @"DocumentItems\[(\d+)\]";
        var match = System.Text.RegularExpressions.Regex.Match(ParamPath, pattern);

        if (match.Success && int.TryParse(match.Groups[1].Value, out int lineNumber))
        {
            return lineNumber + 1;
        }

        return null;
    }
}