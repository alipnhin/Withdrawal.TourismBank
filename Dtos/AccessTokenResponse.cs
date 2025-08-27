namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;

internal class AccessTokenResponse : BaseResponse
{
    public string Access_Token { get; set; }
    public string Token_Type { get; set; }
    public int Expires_In { get; set; }
    public string Scope { get; set; }
    public object Ip { get; set; }
    public int iat { get; set; }
}



internal class FailedAccessTokenResponse 
{
    public string Error { get; set; }
    public string Error_description { get; set; }
}
