using System.Net;
using System.Text.Json;
using HRM.Application.Abstractions.Notifications;
using Microsoft.Extensions.Options;
using WebPush;

namespace HRM.Infrastructure.Services.WebPush;

/// <summary>
/// Gui payload da toi gian qua Web Push protocol/VAPID. Khong log subscription endpoint hoac key.
/// </summary>
public sealed class WebPushSender : IWebPushSender
{
    private readonly WebPushOptions _options;

    public WebPushSender(IOptions<WebPushOptions> options)
    {
        _options = options.Value;
    }

    public bool IsEnabled => _options.Enabled;

    public string PublicKey => _options.Enabled ? _options.VapidPublicKey : string.Empty;

    public async Task<WebPushSendResult> SendAsync(
        WebPushSubscriptionData subscription,
        WebPushPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new WebPushSendResult(WebPushSendStatus.Disabled);
        }

        var pushSubscription = new PushSubscription(
            subscription.Endpoint,
            subscription.P256dh,
            subscription.Auth);
        var vapidDetails = new VapidDetails(
            _options.VapidSubject,
            _options.VapidPublicKey,
            _options.VapidPrivateKey);

        using var client = new WebPushClient();

        try
        {
            await client.SendNotificationAsync(
                pushSubscription,
                JsonSerializer.Serialize(payload),
                vapidDetails,
                cancellationToken);

            return WebPushSendResult.Success();
        }
        catch (WebPushException exception)
        {
            var statusCode = (int)exception.StatusCode;
            return new WebPushSendResult(MapStatus(exception.StatusCode), statusCode);
        }
        catch (HttpRequestException)
        {
            return new WebPushSendResult(WebPushSendStatus.TransientFailure);
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException or JsonException)
        {
            return new WebPushSendResult(WebPushSendStatus.PermanentFailure);
        }
    }

    private static WebPushSendStatus MapStatus(HttpStatusCode statusCode)
    {
        if (statusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return WebPushSendStatus.SubscriptionExpired;
        }

        var numericStatusCode = (int)statusCode;
        return statusCode == HttpStatusCode.RequestTimeout ||
               numericStatusCode == 429 ||
               numericStatusCode >= 500
            ? WebPushSendStatus.TransientFailure
            : WebPushSendStatus.PermanentFailure;
    }
}
