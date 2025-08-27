
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TPG.SI.TadbirPay.Infrastructure.Abstractions.Service;
using TPG.SI.TadbirPay.Infrastructure.Helper;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Services;
using TPG.SI.TadbirPay.Withdrawal.TourismBank.Workflows;

namespace TPG.SI.TadbirPay.Withdrawal.TourismBank;

public static class ServiceRegistration
{
    /// <summary>
    /// ثبت سرویس‌های بانک گردشگری
    /// </summary>
    public static IServiceCollection AddTourismBankServices(this IServiceCollection services)
    {
        // ثبت تنظیمات بانک گردشگری از فایل پیکربندی
        services.AddOptions<GardeshgariBankOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                configuration.GetSection("GardeshgariBankOptions").Bind(options))
            .PostConfigure(options =>
            {
                // اعتبارسنجی تنظیمات
                if (!options.IsValid())
                {
                    var errors = options.GetValidationErrors();
                    throw new InvalidOperationException(
                        $"تنظیمات GardeshgariBankOptions نامعتبر است: {string.Join(", ", errors)}");
                }
            })
            .ValidateOnStart(); // اعتبارسنجی در startup

        // ثبت سرویس‌های بانک گردشگری
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<GardeshgariPaymentWorkflow>();
        services.AddScoped<IPaymentProviderService, GardeshgariPaymentProviderService>();

        services.AddFinancialServiceClient(
            "Gardeshgari Bank Client",
            "Withdrawal Services",
            "GardeshgariBankClient",
            configureClient: client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                client.DefaultRequestHeaders.Add("User-Agent", "TadbirPay-GardeshgariProvider/1.0");
            });



        return services;
    }

    /// <summary>
    /// ثبت سرویس‌های بانک گردشگری با تنظیمات سفارشی
    /// </summary>
    public static IServiceCollection AddTourismBankServices(
        this IServiceCollection services,
        Action<GardeshgariBankOptions> configureOptions)
    {
        // اعمال تنظیمات سفارشی
        services.Configure(configureOptions);

        // ثبت سایر سرویس‌ها
        return services.AddTourismBankServices();
    }

    /// <summary>
    /// بررسی سلامت سرویس‌های بانک گردشگری
    /// </summary>
    public static IServiceCollection AddTourismBankHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<GardeshgariBankHealthCheck>("gardeshgari-bank",
                tags: new[] { "bank", "gardeshgari", "external" });

        return services;
    }
}

/// <summary>
/// Health Check برای بانک گردشگری
/// </summary>
public class GardeshgariBankHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<GardeshgariBankOptions> _options;

    public GardeshgariBankHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<GardeshgariBankOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GardeshgariBankClient");

            // بررسی دسترسی به API
            var response = await client.GetAsync(_options.Value.BaseApiUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("بانک گردشگری در دسترس است");
            }

            return HealthCheckResult.Degraded($"بانک گردشگری پاسخ غیرعادی داد: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("بانک گردشگری در دسترس نیست", ex);
        }
    }
}

/// <summary>
/// Extension برای اضافه کردن به Program.cs
/// </summary>
public static class ProgramExtensions
{
    /// <summary>
    /// اضافه کردن کامل سرویس‌های بانک گردشگری
    /// </summary>
    public static IServiceCollection AddCompleteTourismBankServices(
        this IServiceCollection services)
    {
        return services
            .AddTourismBankServices()
            .AddTourismBankHealthChecks();
    }
}