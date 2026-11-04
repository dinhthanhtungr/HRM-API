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
        var linked = from n in candidates
                    join link in links on n.Id equals link.NotificationId
                    where link.ConversationId.HasValue
                    select new { n.Id, n.CreatedDate, link.ConversationId };
        var latestDates = linked.GroupBy(n => n.ConversationId)
            .Select(group => new { ConversationId = group.Key, CreatedDate = group.Max(n => n.CreatedDate) });
        var latestIds = from n in linked
                        join date in latestDates on new { n.ConversationId, n.CreatedDate }
                            equals new { date.ConversationId, date.CreatedDate }
                        where !linked.Any(other => other.ConversationId == n.ConversationId &&
                            other.CreatedDate == n.CreatedDate && other.Id.CompareTo(n.Id) > 0)
                        select n.Id;
        if (businessOnly)
            visible = visible.Where(n => !messageTopics.Contains(n.Topic));

        // Pick the winner BEFORE category, event-group and cursor filters. Chat does not
        // displace the latest business event; message/all tabs use the actual latest event.
        // Compute winners as a set instead of repeating the JSON link/newer-event
        // lookup for every candidate. The outer query still enforces visibility.
        return visible.Where(n => latestIds.Contains(n.Id) ||
            links.Any(link => link.NotificationId == n.Id && !link.HasConversation));
    }
}
