namespace HRM.Application.Abstractions.Persistence.InternalMail;

/// <summary>Read model over existing notification JSON; no table or EF entity registration.</summary>
public sealed class InternalMailNotificationLink
{
    public Guid NotificationId { get; set; }
    public Guid? ConversationId { get; set; }
    public bool HasConversation { get; set; }
}
