using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Only the scalar fields needed to select a list icon, never notification content.</summary>
internal sealed class InternalMailBusinessEvent
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public DateTime CreatedDate { get; init; }
    public TopicNotifications Topic { get; init; }
}
