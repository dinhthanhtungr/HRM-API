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
        string? eventGroupCode,
        Guid[]? candidateConversationIds = null)
    {
        var messageTopics = NotificationTopicCatalog.GetTopics(null, "message").ToArray();
        var businessOnly = eventGroupCode is not null && eventGroupCode != "message";
        var candidates = businessOnly ? inbox.Where(n => !messageTopics.Contains(n.Topic)) : inbox;
        var linked = from n in candidates
                    join link in links on n.Id equals link.NotificationId
                    where link.ConversationId.HasValue
                    select new { n.Id, n.CreatedDate, link.ConversationId };
        // Restrict threads, not topics: all their events still compete for the winner.
        // Filtering topics before winner selection could resurrect an old business event.
        if (candidateConversationIds is not null)
            linked = linked.Where(n => candidateConversationIds.Contains(n.ConversationId!.Value));
        var latestDates = linked.GroupBy(n => n.ConversationId)
            .Select(group => new { ConversationId = group.Key, CreatedDate = group.Max(n => n.CreatedDate) });
        // Canonical UUID text has the same ordering as the UUID tie-breaker. Aggregate
        // only the tied newest rows instead of probing all linked history per row.
        var latestIds = (from n in linked
                         join date in latestDates on new { n.ConversationId, n.CreatedDate }
                             equals new { date.ConversationId, date.CreatedDate }
                         select n)
            .GroupBy(n => n.ConversationId)
            .Select(group => group.Max(n => n.Id.ToString()));
        if (businessOnly)
            visible = visible.Where(n => !messageTopics.Contains(n.Topic));

        // Pick the winner BEFORE category, event-group and cursor filters. Chat does not
        // displace the latest business event; message/all tabs use the actual latest event.
        // Compute winners as a set instead of repeating the JSON link/newer-event
        // lookup for every candidate. The outer query still enforces visibility.
        return visible.Where(n => latestIds.Contains(n.Id.ToString()) ||
            links.Any(link => link.NotificationId == n.Id && !link.HasConversation));
    }
}
