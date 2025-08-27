namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;



/// <summary>
/// مدل پاسخ API
/// </summary>
internal class ApiResponse<T>
{
    public bool Success { get; set; }
    public T Data { get; set; }
    public string ErrorMessage { get; set; }
    public int StatusCode { get; set; }
    public List<ErrorItem> ErrorList { get; set; }
}
