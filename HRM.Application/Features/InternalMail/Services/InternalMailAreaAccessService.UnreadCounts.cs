using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    internal async Task<Dictionary<Guid, int>> PageNotificationUnreadCountsAsync(Guid[] conversationIds, CancellationToken ct,
        IReadOnlyDictionary<Guid, int>? requestLegacyCounts = null)
    {
        if (conversationIds.Length == 0) return [];
        // Materialize only the authorized page keys. Keep the notification and legacy
        // sources disjoint, as in NotificationUnreadCounts, without one large UNION plan.
        var allowedIds = await ConversationsForCandidates(conversationIds)
            .Select(c => c.InternalConversationId).ToArrayAsync(ct);
        if (allowedIds.Length == 0) return [];
        var counts = await db.QueryInternalConversationNotificationUnreadCounts(
                user.CompanyId ?? Guid.Empty, user.EmployeeId ?? Guid.Empty, allowedIds,
                allowCompanyObserver: CanObserveCompanyConversations)
            .ToDictionaryAsync(c => c.ConversationId, c => c.UnreadCount, ct);
        // A caller may reuse its earlier authorized legacy read in this request.
        // The page's current conversation scope is still checked above; nothing is cached across requests.
        var legacy = requestLegacyCounts ?? await LegacyUnreadCountsByConversationAsync(ct, allowedIds);
        var allowed = allowedIds.ToHashSet();
        foreach (var pair in legacy)
            if (allowed.Contains(pair.Key)) counts[pair.Key] = counts.GetValueOrDefault(pair.Key) + pair.Value;
        return counts;
    }
}
