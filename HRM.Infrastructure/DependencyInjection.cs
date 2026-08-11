using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Ais.CRM;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Features.Attachments.Services;
using HRM.Infrastructure.Services.ExternalIds;
using HRM.Infrastructure.Services.FileStorage;
using HRM.Infrastructure.Services.Geminis;
using HRM.Infrastructure.Services.Geminis.CRM.CustomerCare.InteractionSummaries;
using HRM.Infrastructure.Services.Time;
using HRM.Infrastructure.Services.WebPush;
using HRM.Application.Abstractions.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HRM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddPersistence(configuration)
            .AddAuthenticationServices();

        services.AddScoped<IExternalIdService, ExternalIdServicePostgres>();
        services.Configure<StorageOptions>(options =>
        {
            var section = configuration.GetSection("Storage");
            options.RootPath = section["RootPath"] ?? string.Empty;
            options.PublicBaseUrl = section["PublicBaseUrl"];
        });
        services.AddScoped<IFileStorage, FileShareStorage>();
        services.AddSingleton<IImageThumbnailGenerator, ImageSharpThumbnailGenerator>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        var webPushSection = configuration.GetSection("WebPush");
        var webPushOptions = new WebPushOptions
        {
            Enabled = bool.TryParse(webPushSection["Enabled"], out var webPushEnabled) && webPushEnabled,
            VapidSubject = webPushSection["VapidSubject"] ?? string.Empty,
            VapidPublicKey = webPushSection["VapidPublicKey"] ?? string.Empty,
            VapidPrivateKey = webPushSection["VapidPrivateKey"] ?? string.Empty
        };

        if (webPushOptions.Enabled &&
            (string.IsNullOrWhiteSpace(webPushOptions.VapidSubject) ||
             string.IsNullOrWhiteSpace(webPushOptions.VapidPublicKey) ||
             string.IsNullOrWhiteSpace(webPushOptions.VapidPrivateKey)))
        {
            throw new InvalidOperationException(
                "WebPush VAPID subject, public key and private key are required when WebPush is enabled.");
        }

        services.AddSingleton<IOptions<WebPushOptions>>(Options.Create(webPushOptions));
        services.AddSingleton<IWebPushSender, WebPushSender>();
        var geminiSection = configuration.GetSection("Gemini");
        var geminiOptions = new GeminiOptions
        {
            Enabled = bool.TryParse(geminiSection["Enabled"], out var enabled) && enabled,
            ApiKey = geminiSection["ApiKey"] ?? string.Empty,
            BaseUrl = geminiSection["BaseUrl"] ?? "https://generativelanguage.googleapis.com",
            Model = geminiSection["Model"] ?? "gemini-2.5-flash-lite",
            TimeoutSeconds = int.TryParse(geminiSection["TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : 120,
            RateLimit = new GeminiRateLimitOptions
            {
                RequestsPerMinute = int.TryParse(geminiSection["RateLimit:RequestsPerMinute"], out var rpm) ? rpm : 15,
                RequestsPerDay = int.TryParse(geminiSection["RateLimit:RequestsPerDay"], out var rpd) ? rpd : 500
            }
        };

        services.AddSingleton<IOptions<GeminiOptions>>(Options.Create(geminiOptions));
        services.AddSingleton<IGeminiRateLimitService, GeminiRateLimitService>();
        services.AddScoped<ICustomerInteractionAiSummaryClient>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds))
            };

            return new GeminiCustomerSummaryClient(
                client,
                provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>());
        });

        return services;
    }
}
