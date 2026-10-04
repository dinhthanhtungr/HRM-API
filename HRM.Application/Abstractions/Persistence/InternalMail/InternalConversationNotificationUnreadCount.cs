namespace HRM.Application.Abstractions.Persistence.InternalMail;

/// <summary>Unread inbox notifications for one authorized conversation, before feed pagination.</summary>
public sealed class InternalConversationNotificationUnreadCount
{
    public Guid ConversationId { get; set; }
    public int UnreadCount { get; set; }
}
