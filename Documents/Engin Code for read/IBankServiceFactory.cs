using TPG.SI.TadbirPay.Infrastructure.Abstractions.Service;
using TPG.SI.TadbirPay.Infrastructure.Enums;

namespace TPG.SI.TadbirPay.Services.Services.WithdrawalPayment
{
    public interface IBankServiceFactory
    {
        IPaymentProviderService GetBankService(BankEnum bankKey);
    }
}
