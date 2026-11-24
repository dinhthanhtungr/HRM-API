using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    internal async Task<IQueryable<Notification>> LatestUnreadHubEventsAsync(
        IQueryable<Notification> visible, IQueryable<Notification> inbox,
        string? eventGroupCode, Guid[] conversationIds, CancellationToken ct, Guid[]? unreadStandaloneIds = null)
    {
        const int maxCandidates = 4096;
        var links = db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty);
        var messageTopics = NotificationTopicCatalog.GetTopics(null, "message").ToArray();
        var businessOnly = eventGroupCode is not null && eventGroupCode != "message";
        var candidates = businessOnly ? inbox.Where(n => !messageTopics.Contains(n.Topic)) : inbox;
        var rows = await (from n in candidates join link in links on n.Id equals link.NotificationId
                          where link.ConversationId.HasValue && conversationIds.Contains(link.ConversationId.Value)
                          select new { n.Id, n.CreatedDate, link.ConversationId })
            .Take(maxCandidates + 1).ToArrayAsync(ct);
        if (rows.Length > maxCandidates)
            return LatestHubEvents(visible, inbox, eventGroupCode, conversationIds);
        var standaloneIds = unreadStandaloneIds ?? await candidates
            .Where(n => links.Any(l => l.NotificationId == n.Id && !l.HasConversation))
            .Select(n => n.Id).Take(maxCandidates + 1).ToArrayAsync(ct);
        if (standaloneIds.Length > maxCandidates)
            return LatestHubEvents(visible, inbox, eventGroupCode, conversationIds);
        var winners = rows.GroupBy(n => n.ConversationId)
            .Select(g => g.OrderByDescending(n => n.CreatedDate)
                .ThenByDescending(n => n.Id.ToString(), StringComparer.Ordinal).First().Id)
            .Concat(standaloneIds).Distinct().ToArray();
        if (businessOnly) visible = visible.Where(n => !messageTopics.Contains(n.Topic));
        // Full inbox history competes; the final query retains visibility and unread scope.
        return visible.Where(n => winners.Contains(n.Id));
    }

    internal async Task<(IQueryable<Notification> Visible, Guid[]? StandaloneIds)> UnreadHubEventsForConversationsAsync(
        IQueryable<Notification> visible, IQueryable<Notification> inbox, Guid[] conversationIds, CancellationToken ct,
        Guid[]? suppressedNotificationIds = null)
    {
        const int maxStandaloneCandidates = 4096;
        var links = db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty);
        // The unread-key query already computed this complete set within the same request.
        // A null set means the shortlist overflowed and we still need the SQL predicate.
        var badgeInbox = suppressedNotificationIds is null
            ? BadgeNotifications(inbox)
            : inbox.Where(n => !suppressedNotificationIds.Contains(n.Id));
        var standaloneIds = await badgeInbox.Where(n => n.UserStates.Any(s =>
                s.UserId == user.EmployeeId && !s.IsRead && !s.IsArchived) &&
                !links.Any(l => l.NotificationId == n.Id && l.HasConversation))
            .Select(n => n.Id).Take(maxStandaloneCandidates + 1).ToArrayAsync(ct);
        if (standaloneIds.Length > maxStandaloneCandidates)
            return (UnreadHubEventsForConversations(visible, inbox, conversationIds), null);
        // Preserve the complete visible scope; only the standalone unread set is staged.
        return (visible.Where(n => standaloneIds.Contains(n.Id) || links.Any(l =>
            l.NotificationId == n.Id && l.ConversationId.HasValue && conversationIds.Contains(l.ConversationId.Value))), standaloneIds);
    }

    internal async Task<(Guid[] ConversationIds, IReadOnlyDictionary<Guid, int>? LegacyCounts, Guid[]? SuppressedNotificationIds)> UnreadHubConversationIdsAsync(
        IQueryable<Notification> inbox, CancellationToken ct)
    {
        const int maxSuppressedCandidates = 4096;
        var suppressed = await db.QueryQuotationBadgeSuppressedNotificationIds(
                user.CompanyId ?? Guid.Empty, user.EmployeeId ?? Guid.Empty)
            .Take(maxSuppressedCandidates + 1).ToArrayAsync(ct);
        if (suppressed.Length > maxSuppressedCandidates)
            return (await UnreadHubConversationIds(inbox).ToArrayAsync(ct), null, null);
        var ownUnreadIds = inbox.Where(n => !suppressed.Contains(n.Id) && n.UserStates.Any(s =>
            s.UserId == user.EmployeeId && !s.IsRead && !s.IsArchived)).Select(n => n.Id);
        var linked = await db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty)
            .Where(l => l.ConversationId.HasValue && ownUnreadIds.Contains(l.NotificationId))
            .Select(l => l.ConversationId!.Value).Distinct().ToArrayAsync(ct);
        var legacy = await LegacyUnreadCountsByConversationAsync(ct);
        return (linked.Concat(legacy.Keys).Distinct().ToArray(), legacy, suppressed);
    }

    internal async Task<Guid[]> UnreadConversationIdsAsync(InternalMailRelatedType? relatedType, CancellationToken ct)
    {
        // GROUP-STORAGE-TRANSITION: switch — retain the measured legacy plan until that storage mode is retired.
        if (!UsesGroupStorage) return await UnreadConversationIds(relatedType).Distinct().ToArrayAsync(ct);
        var employee = user.EmployeeId ?? Guid.Empty;
        var company = user.CompanyId ?? Guid.Empty;
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        var ownUnread = db.NotificationUserStates.Where(s => s.UserId == employee && !s.IsRead && !s.IsArchived &&
            s.Notification.CompanyId == company && !reactions.Contains(s.Notification.Topic));
        // Reduce deliveries to distinct conversation keys in SQL before expanding the authorization graph.
        // Reaction notices are excluded above; their author/actor visibility does not apply to this inbox.
        var notificationIds = ownUnread.Select(s => s.NotificationId);
        var eligibleIds = BadgeNotifications(db.Notifications.Where(n => notificationIds.Contains(n.Id))).Select(n => n.Id);
        var links = db.QueryInternalMailNotificationLinks(company);
        var linkedIds = await links.Where(l => eligibleIds.Contains(l.NotificationId) && l.ConversationId.HasValue)
            .Select(l => l.ConversationId!.Value).Distinct().ToArrayAsync(ct);
        var keys = new HashSet<Guid>();
        if (linkedIds.Length != 0)
        {
            // Same full scope as Notifications() for non-reaction linked events, plus unread-key membership.
            var memberIds = await Conversations().Where(c => linkedIds.Contains(c.InternalConversationId) &&
                (CanObserveCompanyConversations || c.Participants.Any(p => p.EmployeeId == employee && p.IsActive)))
                .Select(c => c.InternalConversationId).ToArrayAsync(ct);
            keys.UnionWith(memberIds);
        }
        var legacy = await LegacyUnreadCountsByConversationAsync(ct);
        keys.UnionWith(legacy.Keys);
        if (keys.Count == 0) return [];
        if (!relatedType.HasValue) return keys.ToArray();
        var all = keys.ToArray();
        var type = relatedType.Value;
        return await db.InternalConversations.Where(c => c.CompanyId == company && all.Contains(c.InternalConversationId) &&
            (c.RelatedType == type ||
             ((c.RelatedType == InternalMailRelatedType.ConversationTechnical || c.RelatedType == InternalMailRelatedType.ConversationPricing ||
               c.RelatedType == InternalMailRelatedType.ConversationCustom) &&
              db.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId && parent.CompanyId == company && parent.RelatedType == type))))
            .Select(c => c.InternalConversationId).ToArrayAsync(ct);
    }
}
