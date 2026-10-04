using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.NotificationHub.Queries.GetItems;

internal static class NotificationHubLatestEventQuery
{
    // Both inputs contain only the current employee's non-archived company inbox.
    public static IQueryable<Notification> Apply(
        IQueryable<Notification> visible,
        IQueryable<Notification> inbox,
        IQueryable<InternalMailNotificationLink> links,
        string? eventGroupCode)
    {
        var messageTopics = NotificationTopicCatalog.GetTopics(null, "message").ToArray();
        var businessOnly = eventGroupCode is not null && eventGroupCode != "message";
        var candidates = businessOnly ? inbox.Where(n => !messageTopics.Contains(n.Topic)) : inbox;
        var newer = from n in candidates
                    join link in links on n.Id equals link.NotificationId
                    select new { Notification = n, link.ConversationId };
        if (businessOnly)
            visible = visible.Where(n => !messageTopics.Contains(n.Topic));

        // Pick the winner BEFORE category, event-group and cursor filters. Chat does not
        // displace the latest business event; message/all tabs use the actual latest event.
        return visible.Where(n => links.Any(link => link.NotificationId == n.Id &&
            (!link.HasConversation || (link.ConversationId.HasValue && !newer.Any(other =>
                other.ConversationId == link.ConversationId &&
                (other.Notification.CreatedDate > n.CreatedDate ||
                 (other.Notification.CreatedDate == n.CreatedDate && other.Notification.Id.CompareTo(n.Id) > 0)))))));
    }
}
