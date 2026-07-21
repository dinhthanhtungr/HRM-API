namespace HRM.Application.Features.Notifications.Dtos;

/// <summary>
/// Web Push worker tu resolve nguoi nhan that qua NotificationUserState, nen outbox chi can notificationId.
/// </summary>
public sealed class WebPushOutboxPayload
{
    public Guid NotificationId { get; set; }
}
