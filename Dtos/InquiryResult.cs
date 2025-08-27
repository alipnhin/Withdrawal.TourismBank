using TPG.SI.TadbirPay.Infrastructure.Enums;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;


internal class InquiryResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
    public string ErrorCode { get; set; }
    public string Status { get; set; }
    public string TotalAmount { get; set; }
    //public string TotalValidAmount { get; set; }
    public string TotalInternalAmount { get; set; }
    public string TotalPayaAmount { get; set; }
    public string TotalSatnaAmount { get; set; }
    public List<TransactionResult> Transactions { get; set; } = new List<TransactionResult>();

    public PaymentStatusEnum MapOrderStatus()
    {
        if (string.IsNullOrEmpty(Status))
            return PaymentStatusEnum.SubmittedToBank;

        switch (Status.ToUpper())
        {
            case "DONE":
                return PaymentStatusEnum.BankSucceeded;
            case "FAILED":
                return PaymentStatusEnum.BankRejected;
            case "GROUP_PAYMENT_WAITING_STATE":
            case "GROUP_PAYMENT_REGISTERED_STATE":
            case "GROUP_PAYMENT_UPLOADING_STATE":
                return PaymentStatusEnum.SubmittedToBank;
            case "GROUP_PAYMENT_ERROR_STATE":
                return PaymentStatusEnum.BankRejected;
            case "CANCELED":
                return PaymentStatusEnum.Canceled;
            case "EXPIRED":
                return PaymentStatusEnum.Expired;
            default:
                return PaymentStatusEnum.SubmittedToBank;
        }
    }

    public PaymentItemStatusEnum MapTransactionStatus(string status)
    {
        if (string.IsNullOrEmpty(status))
            return PaymentItemStatusEnum.WaitForBank;

        switch (status.ToUpper())
        {
            case "DONE":
                return PaymentItemStatusEnum.BankSucceeded;
            case "FAIL":
            case "FAILED":
                return PaymentItemStatusEnum.BankRejected;
            case "TODO":
                return PaymentItemStatusEnum.WaitForExecution;
            case "INPROGRESS":
                return PaymentItemStatusEnum.WaitForBank;
            case "REGISTERED":
                return PaymentItemStatusEnum.Registered;
            case "ROLLBACK":
            case "REVERSED":
                return PaymentItemStatusEnum.TransactionRollback;
            case "CANCELED":
                return PaymentItemStatusEnum.Canceled;
            case "EXPIRED":
                return PaymentItemStatusEnum.Expired;
            case "UNKNOWN":
            default:
                return PaymentItemStatusEnum.WaitForBank;
        }
    }

    public TransactionResult FindTransaction(int lineNumber)
    {
        return Transactions?.FirstOrDefault(t => t.LineNumber == lineNumber);
    }
}
