namespace HRM.Application.Abstractions.Notifications;

/// <summary>
/// Cong gui Web Push. Application chi biet contract, implementation VAPID nam o Infrastructure.
/// </summary>
public interface IWebPushSender
{
    bool IsEnabled { get; }

    string PublicKey { get; }

    Task<WebPushSendResult> SendAsync(
        WebPushSubscriptionData subscription,
        WebPushPayload payload,
        CancellationToken cancellationToken = default);
}

public sealed record WebPushSubscriptionData(
    string Endpoint,
    string P256dh,
    string Auth);

public sealed record WebPushPayload(
    Guid NotificationId,
    string Title,
    string Link);

public sealed record WebPushSendResult(
    WebPushSendStatus Status,
    int? HttpStatusCode = null)
{
    public static WebPushSendResult Success() => new(WebPushSendStatus.Success);
}

public enum WebPushSendStatus
{
    Success = 0,
    Disabled = 1,
    SubscriptionExpired = 2,
    TransientFailure = 3,
    PermanentFailure = 4
}
