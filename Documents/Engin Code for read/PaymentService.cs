using Azure;
using Microsoft.Extensions.Logging;
using TPG.SI.SplunkLogger.Utils;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Gateway;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Payment;
using TPG.SI.TadbirPay.Infrastructure.Enums;
using TPG.SI.TadbirPay.Services.Abstractions.Dto;
using TPG.SI.TadbirPay.Services.Abstractions.Service;
using TPG.SI.TadbirPay.Services.Dtos.V01_00.WithdrawalOrder;
using TPG.SI.TadbirPay.Services.Dtos.V01_00.WithdrawalTransaction;
using TPG.SI.TadbirPay.Services.Services.V01_00.Bank;
using TPG.SI.TadbirPay.Services.Services.V01_00.Withdrawal;
using TPG.SI.TadbirPay.Services.Services.V01_00.WithdrawalTransaction;

namespace TPG.SI.TadbirPay.Services.Services.WithdrawalPayment;

internal sealed class PaymentService : ServiceBase, IPaymentService
{
    private ILogger<PaymentService> _logger;
    private readonly IBankGatewayService _gatewayService;
    private readonly IWithdrawalOrderLogService _orderLogService;
    private readonly IWithdrawalOrderService _withdrawalOrderService;
    private readonly IWithdrawalTransactionService _transactionService;
    private readonly IWithdrawalTransactionLogService _transactionLogService;
    private readonly IBankServiceFactory _bankServiceFactory;
    public PaymentService(ILogger<PaymentService> logger,
                          IServiceProvider serviceProvider,
                          IBankGatewayService gatewayService,
                          IWithdrawalOrderLogService orderLogService,
                          IWithdrawalOrderService withdrawalOrderService,
                          IWithdrawalTransactionService transactionService,
                          IBankServiceFactory bankServiceFactory,
                          IWithdrawalTransactionLogService transactionLogService) : base(serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gatewayService = gatewayService ?? throw new ArgumentNullException(nameof(gatewayService));
        _orderLogService = orderLogService ?? throw new ArgumentNullException(nameof(orderLogService));
        _bankServiceFactory = bankServiceFactory ?? throw new ArgumentNullException(nameof(bankServiceFactory));
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _transactionLogService = transactionLogService ?? throw new ArgumentNullException(nameof(transactionLogService));
        _withdrawalOrderService = withdrawalOrderService ?? throw new ArgumentNullException(nameof(withdrawalOrderService));
    }


    private const string _moduleName = "Withdrawal Main Service";

    public string GetHello(BankEnum providerCode)
    {
        var service = _bankServiceFactory.GetBankService(providerCode);
        return service.SayHello();
    }


    public async Task<ServiceResponseDto> PaymentAsync(Guid orderId)
    {

        var paymentResponse = new ServiceResponseDto();
        var order = await _withdrawalOrderService.FindForPaymentAsync(orderId);
        _logger.LogInformationSplunk($"Start proccess payment order with OrderId: {order.OrderId}", order.OrderId, _moduleName);
        //validate sendable
        bool isPayable = CheckPayable(order.Status);
        if (!isPayable)
        {
            paymentResponse.IsSuccess = false;
            paymentResponse.Message = "فقط درخواستهایی که تائید شده اند قابل پرداخت می باشند.";
            return paymentResponse;
        }

        var bankService = _bankServiceFactory.GetBankService(order.ProviderCode);

        if (bankService.HasBatchTransfer)
        {
            paymentResponse = await HandleBatchTransferAsync(order);
        }
        else
        {
            paymentResponse = await HandleSingleTransferAsync(order);
        }

        return paymentResponse;
    }

    private async Task<ServiceResponseDto> HandleSingleTransferAsync(PaymentOrderDto order)
    {
        var getawayInfo = await GetGatewayInfoAsync(order.BankGatewayId);
        if (getawayInfo is null)
        {
            _logger.LogWarningSplunk($"Invalid bank gateway for order with OrderId: {order.OrderId}", order.OrderId, _moduleName);
            return new ServiceResponseDto() { IsSuccess = false, Message = "حساب مبدا تراکنش نامعتبر است." };
        }
        //get provider service
        var bankService = _bankServiceFactory.GetBankService(order.ProviderCode);
        int successCount = 0;
        foreach (var transaction in order.Transactions)
        {

            var response = await bankService.SingleTransferAsync(transaction, getawayInfo);
            _logger.LogInformationSplunk("Proccess single transaction response.", transaction.OrderId, _moduleName, response);
            if (response.IsSuccess)
            {
                successCount++;
                transaction.MetaData = response.Data.MetaData;
                transaction.TrackingId = response.Data?.TrackingId;
            }
            transaction.Status = response.Data.Status;
            transaction.ProviderMessage = response.Message;

        }

        if (successCount > 0)
        {
            order.Status = PaymentStatusEnum.SubmittedToBank;
            order.Transactions
                .Where(t => t.Status != PaymentItemStatusEnum.WaitForExecution)
                .ToList()
                .ForEach(t => t.Status = PaymentItemStatusEnum.BankRejected);
        }
        //order.TrackingId = order.TrackingId;
        _logger.LogInformationSplunk("Start update order data from handle single transaction with changed statuses.", order.OrderId, _moduleName, order);
        await _transactionService.UpdateTransactionInfoAsync(order);
        await _withdrawalOrderService.UpdatePaymentInfoAsync(order);

        if (successCount > 0)
        {
            return new ServiceResponseDto() { IsSuccess = true, Message = "دستور پرداخت با موفقیت به بانک ارسال شد." };
        }
        else
        {
            return new ServiceResponseDto() { IsSuccess = false, Message = "عدم تائید تراکنش از سوی بانک. برای اطلاعات بیشتر پیشینه تغییرات را بررسی نمائید." };
        }
    }


    /// <summary>
    /// batch payment 
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    private async Task<ServiceResponseDto> HandleBatchTransferAsync(PaymentOrderDto order)
    {
        var getawayInfo = await GetGatewayInfoAsync(order.BankGatewayId);
        if (getawayInfo is null)
        {
            return new ServiceResponseDto() { IsSuccess = false, Message = "حساب مبدا تراکنش نامعتبر است." };
        }

        //get provider service
        var bankService = _bankServiceFactory.GetBankService(order.ProviderCode);
        //call send to bank
        var response = await bankService.BatchTransferAsync(order, getawayInfo);
        if (response.IsSuccess)
        {
            _logger.LogInformationSplunk($"Bank Success payment. start to update status and info.", order.OrderId, _moduleName, response);
            //update payment oreder and transactions status and info
            await _withdrawalOrderService.UpdatePaymentInfoAsync(response.Data);
            await _transactionService.UpdateTransactionInfoAsync(response.Data);
            _logger.LogInformationSplunk($"Payment updated successfuly", order.OrderId, _moduleName);
        }
        else
        {
            _logger.LogWarningSplunk($"batch payment transfer failed. {response.Message}", order.OrderId, _moduleName, response);
        }

        return new ServiceResponseDto() { IsSuccess = response.IsSuccess, Message = response.Message };
    }





    /// <summary>
    /// استعلام دستور پرداخت به همراه تراکنش ها
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<ServiceResponseDto> InquiryAsync(Guid orderId)
    {
        var order = await _withdrawalOrderService.FindForPaymentAsync(orderId);
        //get provider 
        var bankService = _bankServiceFactory.GetBankService(order.ProviderCode);

        var getawayInfo = await GetGatewayInfoAsync(order.BankGatewayId);
        if (getawayInfo is null)
        {
            return new ServiceResponseDto() { IsSuccess = false, Message = "حساب مبدا تراکنش نامعتبر است." };
        }

        //call inq method
        var response = await bankService.InquiryPaymentAsync(order, getawayInfo);
        //check response
        if (response.IsFailure)
        {
            return new ServiceResponseDto() { IsSuccess = false, Message = response.Message };
        }
        //update order and transaction
        await _withdrawalOrderService.UpdatePaymentInfoAsync(response.Data);
        await _transactionService.UpdateTransactionInfoAsync(response.Data);

        await SetWithdrawalStatusAsync(orderId);
        //return response
        return new ServiceResponseDto() { IsSuccess = response.IsSuccess, Message = response.Message };
    }


    /// <summary>
    /// استعلام وضعیت یک تراکنش
    /// </summary>
    /// <param name="transactionId"></param>
    /// <returns></returns>
    public async Task<ServiceResponseDto> InquiryTransactionAsync(Guid transactionId)
    {

        var transaction = await _transactionService.FindForPaymentAsync(transactionId);
        //get provider 
        var order = await _withdrawalOrderService.FindAsync(transaction.WithdrawalOrderId);
        var bankService = _bankServiceFactory.GetBankService(order.ProviderCode);
        var getawayInfo = await GetGatewayInfoAsync(order.BankGatewayId);
        if (getawayInfo is null)
        {
            _logger.LogWarningSplunk($"gateway invalid for transaction inquiry.", transaction.OrderId, _moduleName);
            return new ServiceResponseDto() { IsSuccess = false, Message = "حساب مبدا تراکنش نامعتبر است." };
        }
        //call inq method
        var response = await bankService.InquiryTransactionAsync(transaction, getawayInfo);
        //check response
        if (response.IsFailure)
        {
            var log = new CreateWithdrawalTransactionLogDto()
            {
                Status = transaction.Status,
                Description = $"استعلام تراکنش و دریافت پاسخ با متن: {response.Message}",
                WithdrawalTransactionId = transaction.Id
            };
            await _transactionLogService.CreateAsync(log);
            return new ServiceResponseDto() { IsSuccess = false, Message = response.Message };
        }
        //update  transaction
        await _transactionService.UpdateTransactionInfoAsync(response.Data);


        await SetWithdrawalStatusAsync(transaction.WithdrawalOrderId);

        //return response
        return new ServiceResponseDto() { IsSuccess = response.IsSuccess, Message = response.Message };
    }

    private async Task SetWithdrawalStatusAsync(Guid withdrawalOrderId)
    {
        var order = await _withdrawalOrderService.FindAsync(withdrawalOrderId);
        var transactionStats = await _transactionService.GetTransactionStatisticsAsync(withdrawalOrderId);
        var status = order.Status;

        bool inProcess = transactionStats.Any(x => x.Status == PaymentItemStatusEnum.WaitForExecution || x.Status == PaymentItemStatusEnum.WaitForBank);
        bool successd = transactionStats.Any(x => x.Status == PaymentItemStatusEnum.BankSucceeded);
        bool hasRejected = transactionStats.Any(x => x.Status == PaymentItemStatusEnum.BankRejected || x.Status == PaymentItemStatusEnum.TransactionRollback);
        bool hasFailed = transactionStats.Any(x => x.Status == PaymentItemStatusEnum.Failed || x.Status == PaymentItemStatusEnum.Canceled || x.Status == PaymentItemStatusEnum.Expired);

        if (inProcess)
        {
            status = PaymentStatusEnum.SubmittedToBank;
        }
        else if (successd && !inProcess && !hasRejected && !hasFailed)
        {
            status = PaymentStatusEnum.BankSucceeded;
        }
        else if (successd && !inProcess && (hasRejected || hasFailed))
        {
            status = PaymentStatusEnum.DoneWithError;
        }
        else if (!successd && !inProcess && (hasRejected || hasFailed))
        {
            status = PaymentStatusEnum.BankRejected;
        }


        if (order.Status != status)
        {
            var dto = new ChangeWithdrawalOrderStatusDto()
            {
                Status = status,
                Id = order.Id,

            };
            await _withdrawalOrderService.ChangeStatusAsync(dto);
        }

    }


    /// <summary>
    /// واگشی اطلاعات درگاه  یا سرویس پرداخت
    /// </summary>
    /// <param name="gatewayId"></param>
    /// <returns></returns>
    private async Task<GatewayInfoDto> GetGatewayInfoAsync(Guid gatewayId)
    {
        var gatewayInfo = await _gatewayService.GetInfoAsync(gatewayId);
        return gatewayInfo;
    }

    //private IPaymentProviderService GetBankServie(BankEnum providerKey)
    //{

    //    IPaymentProviderService service = _paymentProviders.FirstOrDefault(x => x.ProviderKey == providerKey);
    //    if (service is null)
    //    {
    //        _logger.LogWarning($"Bank withdrwal provider service not found with key: {providerKey}");
    //        throw new ArgumentNullException(nameof(providerKey));
    //    }
    //    return service;
    //}

    private bool CheckPayable(PaymentStatusEnum status)
    {
        if (status == PaymentStatusEnum.OwnersApproved)
            return true;

        return false;
    }
}
