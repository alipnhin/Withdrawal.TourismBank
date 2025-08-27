using TPG.SI.TadbirPay.Infrastructure.Abstractions.Service;
using TPG.SI.TadbirPay.Infrastructure.Enums;

namespace TPG.SI.TadbirPay.Services.Services.WithdrawalPayment
{
    public class BankServiceFactory : IBankServiceFactory
    {
        private readonly Dictionary<BankEnum, IPaymentProviderService> _services;

        public BankServiceFactory(IEnumerable<IPaymentProviderService> services)
        {
            _services = services.ToDictionary(service => service.ProviderKey, service => service);
        }

        public IPaymentProviderService GetBankService(BankEnum bankKey)
        {
            if (_services.TryGetValue(bankKey, out var service))
            {
                return service;
            }

            throw new ArgumentException("Bank service not found for the given key.", nameof(bankKey));
        }
    }

}
