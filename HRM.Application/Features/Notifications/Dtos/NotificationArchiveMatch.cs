namespace HRM.Application.Features.Notifications.Dtos;

/// <summary>Uses the same payload interpretation as the inbox; malformed metadata never matches a thread.</summary>
public static class NotificationArchiveMatch
{
    public static bool Conversation(NotificationDto notification, Guid conversationId, Guid? messageId = null)
    {
        NotificationPayloadPresentation.Apply(notification);
        return notification.ConversationId == conversationId &&
            (!messageId.HasValue || notification.MessageId == messageId);
    }

    public static bool Group(NotificationDto notification, NotificationDto anchor)
    {
        NotificationPayloadPresentation.Apply(notification);
        NotificationPayloadPresentation.Apply(anchor);
        if (notification.Id == anchor.Id) return true;
        // Threads have a separate preference/archive action. Never capture their notifications here.
        if (anchor.ConversationId.HasValue || notification.ConversationId.HasValue) return false;
        return anchor.Context?.AggregateId is { } aggregateId &&
            notification.Context?.AggregateId == aggregateId &&
            !string.IsNullOrWhiteSpace(anchor.Context.AggregateType) &&
            string.Equals(notification.Context.AggregateType, anchor.Context.AggregateType, StringComparison.Ordinal);
    }
}
