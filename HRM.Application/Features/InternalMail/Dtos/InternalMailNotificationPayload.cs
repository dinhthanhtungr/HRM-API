namespace HRM.Application.Features.InternalMail.Dtos;

/// <summary>
/// Payload nho de notification tro ve dung conversation/message. SignalR van chi day notificationId.
/// </summary>
public sealed class InternalMailNotificationPayload
{
    public string ContentType { get; set; } = "InternalMailMessage";
    public Guid ConversationId { get; set; }
    public Guid MessageId { get; set; }
    public string? RelatedType { get; set; }
    public Guid? RelatedId { get; set; }
    public bool IsUrgent { get; set; }
}
