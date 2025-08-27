namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;

internal class BaseResponse
{
    public bool IsSuccess { get; set; }
    public int RsCode { get; set; }
    public string Message { get; set; }

    public List<ErrorItem> ErrorList { get; set; }
}


internal class ErrorItem
{
    public int Code { get; set; }

    public string Desc { get; set; }

    public string ParamName { get; set; }

    public string ParamPath { get; set; }

}