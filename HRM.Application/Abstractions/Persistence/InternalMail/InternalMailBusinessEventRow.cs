using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Abstractions.Persistence.InternalMail;

/// <summary>Scalar metadata bound to an exact non-deleted message, not an inbox grant.</summary>
public sealed class InternalMailBusinessEventRow
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public DateTime CreatedDate { get; set; }
    public TopicNotifications Topic { get; set; }
}
