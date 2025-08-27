using TPG.SI.TadbirPay.Infrastructure.Enums;
using TPG.SI.TadbirPay.Services.Abstractions.Dto;
using TPG.SI.TadbirPay.Services.Abstractions.Service;

namespace TPG.SI.TadbirPay.Services.Services.WithdrawalPayment;

public interface IPaymentService : IServiceBase
{
    string GetHello(BankEnum providerKey);

    Task<ServiceResponseDto> PaymentAsync(Guid orderId);

    Task<ServiceResponseDto> InquiryAsync(Guid orderId);

    Task<ServiceResponseDto> InquiryTransactionAsync(Guid transactionId);
}
