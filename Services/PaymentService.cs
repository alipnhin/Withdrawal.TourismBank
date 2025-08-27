using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TPG.SI.SplunkLogger.Utils;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Gateway;
using TPG.SI.TadbirPay.Infrastructure.Dtos.Payment;
using TPG.SI.TadbirPay.Infrastructure.Enums;
using TPG.SI.TadbirPay.Infrastructure.Helper;
using TPG.SI.TadbirPay.Withdrawal.TourismBank;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Dtos.Response;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Helpers;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank.Services;

/// <summary>
/// سرویس پرداخت گروهی بانک گردشگری - نسخه ادغام شده
/// </summary>
internal class PaymentService : IPaymentService
{
    #region Fields & Constants

    private readonly ILogger<PaymentService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IOptions<GardeshgariBankOptions> _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _appModule = "GardeshgariPaymentService";

    /// <summary>
    /// آدرس‌های API
    /// </summary>
    private static class ApiEndpoints
    {
        public const string GroupPaymentRegister = "/GroupPayment/GroupPaymentRegister";
        public const string DoPayment = "/GroupPayment/DoPayment";
        public const string GroupPaymentInquiry = "/GroupPayment/GroupPaymentInquiry";
        public const string InquiryFromCore = "/GroupPayment/GroupPaymentInquiryFromCore";
    }

    #endregion

    #region Constructor

    public PaymentService(
        ILogger<PaymentService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<GardeshgariBankOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClientFactory.CreateClient("GardeshgariBankClient");
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };
    }

    #endregion

    #region Payment Methods

    /// <summary>
    /// ثبت پرداخت گروهی
    /// </summary>
    public async Task<RegisterGroupPaymentResult> RegisterGroupPaymentAsync(PaymentOrderDto paymentOrderDto, GatewayInfoDto gatewayInfo)
    {
        try
        {
            _logger.LogInformationSplunk(
                "Starting group payment registration",
                paymentOrderDto.OrderId,
                _appModule,
                new { TransactionCount = paymentOrderDto.Transactions.Count }
            );

            var metaData = GetMetaData(gatewayInfo);
            if (metaData == null || metaData.CustomerNumber is null)
            {
                return new RegisterGroupPaymentResult
                {
                    IsSuccess = false,
                    Message = "کانفیگ سرویس پرداخت نامعتبر است.",
                };
            }

            // ایجاد DocumentItems برای هر تراکنش
            var documentItems = paymentOrderDto.Transactions
                .Select(transaction => new DocumentItem
                {
                    LineNumber = transaction.RowNumber,
                    Amount = transaction.Amount,
                    DestinationIban = transaction.DestinationIban,
                    TransactionDate = DateHelper.GetPersianTransactionDate(DateTime.Now).ParseToInt(),
                    RecieverFullName = transaction.DestinationAccountOwner,
                    Description = transaction.Description,
                    TransactionBillNumber = null,
                    CauseType = MapReasonCodeToCauseType(transaction.ReasonCode)
                }).ToList();

            string transactionId = GenerateTransactionId(metaData.OrganizationCode);

            // ایجاد بدنه درخواست
            var requestBody = new GroupPaymentRegisterRequest
            {
                TransactionId = transactionId,
                AutoContinue = true,
                CustomerNumber = metaData.CustomerNumber,
                SourceDeposit = gatewayInfo.AccountNumber,
                RefundDeposit = gatewayInfo.AccountNumber,
                SourceDepositCommission = gatewayInfo.AccountNumber,
                SourceDescription = paymentOrderDto.Description,
                DocumentItems = documentItems
            };

            // ارسال درخواست به API و دریافت پاسخ
            var response = await SendRequestAsync<BaseResponse>(
                ApiEndpoints.GroupPaymentRegister,
                requestBody,
                gatewayInfo,
                paymentOrderDto.OrderId);

            if (!response.Success)
            {
                return new RegisterGroupPaymentResult
                {
                    IsSuccess = false,
                    Message = response.ErrorMessage,
                    ErrorCode = response.StatusCode.ToString()
                };
            }

            var registerResponse = response.Data;

            if (!registerResponse.IsSuccess)
            {
                return new RegisterGroupPaymentResult
                {
                    IsSuccess = false,
                    Message = $"خطا در ثبت تراکنش: {registerResponse.Message}"
                };
            }

            return new RegisterGroupPaymentResult
            {
                IsSuccess = true,
                Message = "ثبت تراکنش گروهی با موفقیت انجام شد",
                TrackingId = transactionId
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                $"Exception in register group payment",
                paymentOrderDto.OrderId,
                _appModule,
                ex
            );

            return new RegisterGroupPaymentResult
            {
                IsSuccess = false,
                Message = "خطا در ثبت پرداخت گروهی. لطفا بعدا تلاش کنید.",
                ErrorCode = "SYSTEM_ERROR"
            };
        }
    }

    /// <summary>
    /// اجرای پرداخت گروهی
    /// </summary>
    public async Task<BaseResponse> ExecuteGroupPaymentAsync(string transactionId, GatewayInfoDto gatewayInfo)
    {
        try
        {
            _logger.LogInformationSplunk(
                "Starting group payment execution",
                transactionId,
                _appModule
            );

            var requestBody = new PaymentRequest
            {
                TransactionId = transactionId
            };

            var response = await SendRequestAsync<BaseResponse>(
                ApiEndpoints.DoPayment,
                requestBody,
                gatewayInfo,
                transactionId);

            if (!response.Success)
            {
                return new BaseResponse
                {
                    IsSuccess = false,
                    Message = response.ErrorMessage,
                    RsCode = response.StatusCode
                };
            }

            var executeResponse = response.Data;

            if (!executeResponse.IsSuccess)
            {
                return new BaseResponse
                {
                    IsSuccess = false,
                    Message = $"خطا در اجرای تراکنش: {executeResponse.Message}",
                    RsCode = executeResponse.RsCode
                };
            }

            return new BaseResponse
            {
                IsSuccess = true,
                Message = "اجرای تراکنش گروهی با موفقیت انجام شد"
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                $"Exception in execute group payment",
                transactionId,
                _appModule,
                ex
            );

            return new BaseResponse
            {
                IsSuccess = false,
                Message = "خطا در اجرای پرداخت گروهی. لطفا بعدا تلاش کنید.",
                RsCode = 500
            };
        }
    }

    #endregion

    #region Inquiry Methods

    /// <summary>
    /// استعلام وضعیت پرداخت گروهی - انتخاب هوشمند API
    /// </summary>
    public async Task<InquiryResult> InquiryGroupPaymentAsync(
        GatewayInfoDto gatewayInfo,
        InquiryRequest inquiryRequest)
    {
        try
        {
            _logger.LogInformationSplunk(
                "Starting group payment inquiry",
                inquiryRequest.TransactionId,
                _appModule,
                inquiryRequest
            );

            // انتخاب API مناسب بر اساس نوع درخواست
            string apiEndpoint = DetermineInquiryEndpoint(inquiryRequest);

            _logger.LogInformationSplunk(
                $"Using inquiry endpoint: {apiEndpoint}",
                inquiryRequest.TransactionId,
                _appModule
            );

            // ارسال درخواست به API مناسب
            var response = await SendRequestAsync<InquiryResponse>(
                apiEndpoint,
                inquiryRequest,
                gatewayInfo,
                inquiryRequest.TransactionId);

            if (!response.Success)
            {
                return new InquiryResult
                {
                    IsSuccess = false,
                    Message = response.ErrorMessage,
                    ErrorCode = response.StatusCode.ToString()
                };
            }

            var inquiryResponse = response.Data;
            _logger.LogInformationSplunk(
                "Inquiry response received",
                inquiryRequest.TransactionId,
                _appModule,
                new
                {
                    inquiryResponse.IsSuccess,
                    Status = inquiryResponse.ResultData?.Result?.State,
                    HasTransactions = inquiryResponse.ResultData?.Result != null
                }
            );

            if (!inquiryResponse.IsSuccess)
            {
                return new InquiryResult
                {
                    IsSuccess = false,
                    Message = $"خطا در استعلام تراکنش: {inquiryResponse.Message}",
                    ErrorCode = inquiryResponse.RsCode.ToString()
                };
            }

            // تبدیل پاسخ استعلام به مدل استاندارد
            var result = MapInquiryResponseToResult(inquiryResponse);
            result.IsSuccess = true;
            result.Message = "استعلام پرداخت گروهی با موفقیت انجام شد";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                $"Exception in inquiry group payment",
                inquiryRequest.TransactionId,
                _appModule,
                ex
            );

            return new InquiryResult
            {
                IsSuccess = false,
                Message = "خطا در استعلام پرداخت گروهی. لطفا بعدا تلاش کنید.",
                ErrorCode = "SYSTEM_ERROR"
            };
        }
    }

    /// <summary>
    /// بررسی آمادگی اجرای پرداخت گروهی - API اول
    /// </summary>
    public async Task<ReadinessInquiryResult> CheckExecutionReadinessAsync(
        string transactionId,
        GatewayInfoDto gatewayInfo)
    {
        try
        {
            _logger.LogInformationSplunk(
                "Checking execution readiness",
                transactionId,
                _appModule
            );

            var requestBody = new { TransactionId = transactionId };

            var response = await SendRequestAsync<ReadinessInquiryResponse>(
                ApiEndpoints.GroupPaymentInquiry,
                requestBody,
                gatewayInfo,
                transactionId);

            if (!response.Success)
            {
                return new ReadinessInquiryResult
                {
                    IsSuccess = false,
                    Message = response.ErrorMessage,
                    ErrorCode = response.StatusCode.ToString()
                };
            }

            var inquiryResponse = response.Data;

            if (!inquiryResponse.IsSuccess)
            {
                return new ReadinessInquiryResult
                {
                    IsSuccess = false,
                    Message = inquiryResponse.Message,
                    ErrorCode = inquiryResponse.RsCode.ToString()
                };
            }

            // تبدیل پاسخ به مدل نتیجه
            var result = MapReadinessInquiryResponse(inquiryResponse);
            result.IsSuccess = true;
            result.Message = "بررسی آمادگی با موفقیت انجام شد";

            _logger.LogInformationSplunk(
                $"Readiness check completed. Status: {result.TransactionStatus}",
                transactionId,
                _appModule,
                new
                {
                    Status = result.TransactionStatus,
                    IsReady = result.IsReadyForExecution,
                    ErrorCount = result.RecordErrors.Count
                }
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                "Exception in readiness check",
                transactionId,
                _appModule,
                ex
            );

            return new ReadinessInquiryResult
            {
                IsSuccess = false,
                Message = "خطا در بررسی آمادگی پرداخت گروهی. لطفا بعدا تلاش کنید.",
                ErrorCode = "SYSTEM_ERROR"
            };
        }
    }

    /// <summary>
    /// استعلام جزئیات تراکنش‌های پرداخت گروهی - API دوم
    /// </summary>
    public async Task<TransactionInquiryResult> InquiryTransactionDetailsAsync(
        TransactionInquiryRequest request,
        GatewayInfoDto gatewayInfo)
    {
        try
        {
            _logger.LogInformationSplunk(
                "Inquiring transaction details",
                request.TransactionId,
                _appModule,
                new
                {
                    LineNumber = request.LineNumber,
                    FirstIndex = request.FirstIndex,
                    LastIndex = request.LastIndex
                }
            );

            var response = await SendRequestAsync<TransactionInquiryResponse>(
                ApiEndpoints.InquiryFromCore,
                request,
                gatewayInfo,
                request.TransactionId);

            if (!response.Success)
            {
                return new TransactionInquiryResult
                {
                    IsSuccess = false,
                    Message = response.ErrorMessage,
                    ErrorCode = response.StatusCode.ToString()
                };
            }

            var inquiryResponse = response.Data;

            if (!inquiryResponse.IsSuccess)
            {
                return new TransactionInquiryResult
                {
                    IsSuccess = false,
                    Message = inquiryResponse.Message,
                    ErrorCode = inquiryResponse.RsCode.ToString()
                };
            }

            // تبدیل پاسخ به مدل نتیجه
            var result = MapTransactionInquiryResponse(inquiryResponse, request);
            result.IsSuccess = true;
            result.Message = "استعلام جزئیات تراکنش‌ها با موفقیت انجام شد";

            _logger.LogInformationSplunk(
                $"Transaction details inquiry completed. Found {result.Transactions.Count} transactions",
                request.TransactionId,
                _appModule,
                new { TransactionCount = result.Transactions.Count }
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                "Exception in transaction details inquiry",
                request.TransactionId,
                _appModule,
                ex
            );

            return new TransactionInquiryResult
            {
                IsSuccess = false,
                Message = "خطا در استعلام جزئیات تراکنش‌ها. لطفا بعدا تلاش کنید.",
                ErrorCode = "SYSTEM_ERROR"
            };
        }
    }

    #endregion

    #region Auth & Communication Methods

    /// <summary>
    /// دریافت توکن دسترسی
    /// </summary>
    private async Task<AccessTokenResponse> GetAccessTokenAsync(string clientId, string clientSecret, string branchCode)
    {
        try
        {
            _logger.LogInformationSplunk(
                "Getting access token",
                clientId,
                _appModule
            );

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var request = new HttpRequestMessage(HttpMethod.Post, _options.Value.AccessTokenUrl);

            var collection = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "client_credentials"),
                new("client_id", clientId),
                new("client_secret", clientSecret),
                new("client_claims", "{\"branch_code\":\""+branchCode+"\"}")
            };

            var content = new FormUrlEncodedContent(collection);
            request.Content = content;

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogErrorSplunk(
                    "Error getting access token",
                    clientId,
                    _appModule,
                    new { response.StatusCode, Response = responseContent }
                );
                var failedResponse = JsonSerializer.Deserialize<FailedAccessTokenResponse>(responseContent, _jsonOptions);
                return new AccessTokenResponse
                {
                    IsSuccess = false,
                    Message = failedResponse.Error_description,
                    RsCode = (int)response.StatusCode
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<AccessTokenResponse>(responseContent, _jsonOptions);
            if (tokenResponse == null)
            {
                return new AccessTokenResponse
                {
                    IsSuccess = false,
                    Message = "خطا در پردازش توکن دسترسی"
                };
            }

            tokenResponse.IsSuccess = true;
            return tokenResponse;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                "Exception getting access token",
                clientId,
                _appModule,
                ex
            );

            return new AccessTokenResponse
            {
                IsSuccess = false,
                Message = "خطا در دریافت توکن دسترسی"
            };
        }
    }

    /// <summary>
    /// ارسال درخواست به API و پردازش پاسخ
    /// </summary>
    private async Task<ApiResponse<T>> SendRequestAsync<T>(
        string url,
        object requestBody,
        GatewayInfoDto gatewayInfo,
        string correlationId)
    {
        try
        {
            var endpoint = _options.Value.BaseApiUrl + url;

            var metaData = GetMetaData(gatewayInfo);
            if (metaData == null || metaData.ClientId is null || metaData.ClientSecret is null)
            {
                return new ApiResponse<T>
                {
                    Success = false,
                    ErrorMessage = "کانفیگ سرویس پرداخت نامعتبر است.",
                    StatusCode = 400
                };
            }

            // دریافت توکن دسترسی
            var accessToken = await GetAccessTokenAsync(metaData.ClientId, metaData.ClientSecret, metaData.BranchCode);
            if (!accessToken.IsSuccess)
            {
                return new ApiResponse<T>
                {
                    Success = false,
                    ErrorMessage = accessToken.Message,
                    StatusCode = 401
                };
            }

            // تنظیم هدرهای درخواست
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string body = JsonSerializer.Serialize(requestBody, _jsonOptions);
            string signString = $"POST#{url}#{metaData.ApiKey}#{body}";
            if (url == ApiEndpoints.GroupPaymentRegister)
            {
                var requestDto = (GroupPaymentRegisterRequest)requestBody;
                // httpMethod(Upper Case)#url(after Address Root)#ApiKey# SourceDeposit#TotalCount#TotalAmount
                signString = $"POST#{url}#{metaData.ApiKey}#{gatewayInfo.AccountNumber}#{requestDto.DocumentItems.Count}#{requestDto.DocumentItems.Sum(x => x.Amount.ParseToLong())}";
            }

            string signature = SignData(signString, gatewayInfo.PrivateEncryptionKey);

            _httpClient.DefaultRequestHeaders.Add("ApiKey", metaData.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("Signature", signature);
            _httpClient.DefaultRequestHeaders.Add("Accept-Version", _options.Value.ApiVersion);
            _httpClient.DefaultRequestHeaders.Add("AccessToken", accessToken.Access_Token);

            // ارسال درخواست به بانک
            var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogErrorSplunk(
                    $"Error in API call to {url}",
                    correlationId,
                    _appModule,
                    new { response.StatusCode, ResponseContent = responseContent }
                );

                var failedResponse = JsonSerializer.Deserialize<BaseResponse>(responseContent, _jsonOptions);
                return new ApiResponse<T>
                {
                    Success = false,
                    ErrorMessage = $"خطای سرویس بانک: {failedResponse.Message}, کد خطا:{failedResponse.RsCode}",
                    StatusCode = (int)response.StatusCode
                };
            }

            // تحلیل پاسخ بانک
            var apiResponse = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
            if (apiResponse == null)
            {
                return new ApiResponse<T>
                {
                    Success = false,
                    ErrorMessage = "خطا در دریافت پاسخ از بانک",
                    StatusCode = 500
                };
            }

            return new ApiResponse<T>
            {
                Success = true,
                Data = apiResponse,
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorSplunk(
                $"Exception in API call to {url}",
                correlationId,
                _appModule,
                ex
            );

            return new ApiResponse<T>
            {
                Success = false,
                ErrorMessage = "خطا در ارتباط با سرویس بانک. لطفا بعدا تلاش کنید.",
                StatusCode = 500
            };
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// تعیین endpoint مناسب برای استعلام
    /// </summary>
    private static string DetermineInquiryEndpoint(InquiryRequest request)
    {
        // اگر FirstIndex/LastIndex یا LineNumber مشخص شده، از InquiryFromCore استفاده کن
        if (request.FirstIndex.HasValue && request.LastIndex.HasValue || request.LineNumber.HasValue)
        {
            return ApiEndpoints.InquiryFromCore;
        }

        // در غیر این صورت از Inquiry معمولی استفاده کن
        return ApiEndpoints.GroupPaymentInquiry;
    }

    /// <summary>
    /// تبدیل پاسخ API استعلام به مدل نتیجه
    /// </summary>
    private InquiryResult MapInquiryResponseToResult(InquiryResponse inquiryResponse)
    {
        var result = new InquiryResult
        {
            IsSuccess = inquiryResponse.IsSuccess,
            Message = inquiryResponse.Message,
            ErrorCode = inquiryResponse.RsCode.ToString()
        };

        if (inquiryResponse.ResultData?.Result != null)
        {
            // استفاده از اطلاعات Result object
            var resultObj = inquiryResponse.ResultData.Result;
            result.Status = resultObj.State ?? "";
            result.TotalAmount = resultObj.TotalAmount ?? "0";
            result.TotalInternalAmount = resultObj.TotalInternalAmount ?? "0";
            result.TotalPayaAmount = resultObj.TotalPayaAmount ?? "0";
            result.TotalSatnaAmount = resultObj.TotalSatnaAmount ?? "0";
        }
        else
        {
            // fallback به فیلدهای level بالا
            result.Status = inquiryResponse.ResultData?.Result?.State ?? "";
            result.TotalAmount = inquiryResponse.ResultData?.Result?.TotalAmount ?? "0";
            result.TotalInternalAmount = inquiryResponse.ResultData?.Result?.TotalInternalAmount ?? "0";
            result.TotalPayaAmount = inquiryResponse.ResultData?.Result?.TotalPayaAmount ?? "0";
            result.TotalSatnaAmount = inquiryResponse.ResultData?.Result?.TotalSatnaAmount ?? "0";
        }

        return result;
    }

    /// <summary>
    /// تبدیل پاسخ API اول به مدل نتیجه
    /// </summary>
    private static ReadinessInquiryResult MapReadinessInquiryResponse(ReadinessInquiryResponse response)
    {
        var result = new ReadinessInquiryResult
        {
            TransactionStatus = response.ResultData?.TransactionStatus ?? "",
        };

        // تبدیل اطلاعات خلاصه
        if (response.ResultData?.Result != null)
        {
            var summary = response.ResultData.Result;
            result.PaymentSummary = new PaymentSummaryInfo
            {
                TransactionId = summary.TransactionId,
                CustomerNumber = summary.CustomerNumber,
                State = summary.State,
                DateTime = summary.DateTime,
                LineCount = summary.LineCount,
                TotalAmount = summary.TotalAmount,
                TotalInternalAmount = summary.TotalInternalAmount,
                TotalPayaAmount = summary.TotalPayaAmount,
                TotalSatnaAmount = summary.TotalSatnaAmount,
                SourceDeposit = summary.SourceDeposit,
                RefundDeposit = summary.RefundDeposit,
                SourceDepositCommission = summary.SourceDepositCommission
            };
        }

        // تبدیل خطاهای رکوردها
        if (response.ResultData?.RecordErrorsList != null)
        {
            result.RecordErrors = response.ResultData.RecordErrorsList
                .Select(error => new RecordError
                {
                    Code = error.Code,
                    Description = error.Desc,
                    ParamName = error.ParamName,
                    ParamPath = error.ParamPath
                })
                .ToList();
        }

        return result;
    }

    /// <summary>
    /// تبدیل پاسخ API دوم به مدل نتیجه
    /// </summary>
    private TransactionInquiryResult MapTransactionInquiryResponse(
        TransactionInquiryResponse response,
        TransactionInquiryRequest request)
    {
        var result = new TransactionInquiryResult();

        if (response.ResultData == null)
            return result;

        // اگر استعلام تکی بود
        if (request.LineNumber.HasValue && response.ResultData.SingleResult != null)
        {
            result.Transactions.Add(MapTransactionDetail(response.ResultData.SingleResult, request.LineNumber.Value));
        }
        // اگر استعلام لیستی بود
        else if (response.ResultData.Result != null)
        {
            result.Transactions = response.ResultData.Result
                .Select((transaction, index) => MapTransactionDetail(transaction, request.FirstIndex.GetValueOrDefault(1) + index))
                .ToList();
        }

        return result;
    }

    /// <summary>
    /// تبدیل جزئیات یک تراکنش
    /// </summary>
    private TransactionDetailResult MapTransactionDetail(TransactionDetailResponse transaction, int lineNumber)
    {
        return new TransactionDetailResult
        {
            LineNumber = lineNumber,
            Amount = transaction.Amount,
            FinalState = transaction.FinalState,
            FinalMessage = transaction.FinalMessage,
            Status = transaction.Status,
            TransactionType = transaction.TransactionType,
            PaymentMethod = DeterminePaymentMethod(transaction.TransactionType),
            TransactionDate = transaction.TransactionDate,
            TransactionDescription = transaction.TransactionDescription,
            RefrenceNumber = transaction.RefrenceNumber,
            BranchCode = transaction.BranchCode,
            DocumentNumber = transaction.DocumentNumber,
            DestinationBankCode = transaction.DestinationBankCode,
            DestinationBankName = transaction.DestinationBankName,
            TransactionCommission = transaction.TransactionCommission,
            ErrorDescription = transaction.ErrorDescription
        };
    }

    /// <summary>
    /// امضای دیجیتال داده
    /// </summary>
    internal string SignData(string data, string privateKeyPem)
    {
        // 1. حذف سرصفحه‌ها و پایین‌صفحه‌ها
        var keyLines = privateKeyPem
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Trim();

        // 2. تبدیل به بایت
        var keyBytes = Convert.FromBase64String(keyLines);

        // 3. ساخت RSA از کلید PKCS#8
        using RSA rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(keyBytes, out _);

        // 4. امضای داده
        var byteData = Encoding.UTF8.GetBytes(data);
        var signedData = rsa.SignData(byteData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // 5. بازگشت امضا به‌صورت Base64
        return Convert.ToBase64String(signedData);
    }

    /// <summary>
    /// تولید شناسه تراکنش
    /// </summary>
    public string GenerateTransactionId(string orgCode, int randomLength = 32)
    {
        // 1. تولید رشته تصادفی
        string randomStr = GenerateRandomString(randomLength);

        // 2. تولید تاریخ با دقت میلی‌ثانیه به فرمت yyyyMMddHHmmssfff
        string dateTimeStr = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

        // 3. محاسبه مجموع کد اسکی
        string baseString = orgCode + randomStr + dateTimeStr;
        int sumCharCode = baseString.Sum(c => c);

        // 4. مونتاژ نهایی
        return $"{orgCode}-{randomStr}-{dateTimeStr}-{sumCharCode}";
    }

    /// <summary>
    /// تولید رشته تصادفی
    /// </summary>
    private string GenerateRandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        using var rng = new RNGCryptoServiceProvider();
        var byteBuffer = new byte[length];
        rng.GetBytes(byteBuffer);

        var result = new StringBuilder(length);
        foreach (byte b in byteBuffer)
        {
            result.Append(chars[b % chars.Length]);
        }

        return result.ToString();
    }

    /// <summary>
    /// تعیین نوع روش پرداخت
    /// </summary>
    public PaymentMethodEnum DeterminePaymentMethod(string transactionType)
    {
        if (string.IsNullOrEmpty(transactionType))
            return PaymentMethodEnum.Unknown;

        return transactionType.ToUpper() switch
        {
            "INTERNAL" => PaymentMethodEnum.Internal,
            "PAYA" => PaymentMethodEnum.Paya,
            "SATNA" => PaymentMethodEnum.Satna,
            "CARD" => PaymentMethodEnum.Card,
            _ => PaymentMethodEnum.Unknown
        };
    }

    /// <summary>
    /// دریافت metadata
    /// </summary>
    private MetaDataDto GetMetaData(GatewayInfoDto gatewayInfoDto)
    {
        try
        {
            var data = JsonSerializer.Deserialize<MetaDataDto>(gatewayInfoDto.MetaData);
            return data;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// تبدیل کد علت پرداخت
    /// </summary>
    private int MapReasonCodeToCauseType(TransactionReasonEnum reasonCode)
    {
        return reasonCode switch
        {
            TransactionReasonEnum.SalaryDeposit => 1,
            TransactionReasonEnum.ServicesInsuarance => 2,
            TransactionReasonEnum.Therapeutic => 3,
            TransactionReasonEnum.InvestmentAndBourse => 4,
            TransactionReasonEnum.LegalCurrencyActivities => 5,
            TransactionReasonEnum.DebtPayment => 6,
            TransactionReasonEnum.Retirement => 7,
            TransactionReasonEnum.MovableProperties => 8,
            TransactionReasonEnum.ImmovableProperties => 9,
            TransactionReasonEnum.CashManagement => 10,
            TransactionReasonEnum.CustomsDuties => 11,
            TransactionReasonEnum.TaxSettle => 12,
            TransactionReasonEnum.OtherGovermentServices => 13,
            TransactionReasonEnum.FacilitiesAndCommitments => 14,
            TransactionReasonEnum.BondReturn => 15,
            TransactionReasonEnum.GeneralAndDailyCosts => 16,
            TransactionReasonEnum.Charity => 17,
            TransactionReasonEnum.StuffsPurchase => 18,
            TransactionReasonEnum.ServicesPurchase => 19,
            _ => 16
        };
    }

    #endregion
}