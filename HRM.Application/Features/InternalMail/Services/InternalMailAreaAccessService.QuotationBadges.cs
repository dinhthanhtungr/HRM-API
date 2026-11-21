using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    // Keep recipient/history records; suppress only unread quotation events that do not represent a distinct visible message.
    internal IQueryable<Notification> BadgeNotifications(IQueryable<Notification> source)
    {
        var suppressed = db.QueryQuotationBadgeSuppressedNotificationIds(user.CompanyId ?? Guid.Empty, user.EmployeeId ?? Guid.Empty);
        return source.Where(n => !suppressed.Contains(n.Id));
    }

    internal async Task<Dictionary<Guid, int>> QuotationBadgeCountsAsync(Guid[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return [];
        var quoteIds = await QuotationBadgeConversationIds(ids).ToArrayAsync(ct);
        if (quoteIds.Length == 0) return [];
        var counts = await NotificationUnreadCounts(quoteIds).ToDictionaryAsync(c => c.ConversationId, c => c.UnreadCount, ct);
        return quoteIds.ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }

    internal async Task<Dictionary<Guid, int>> PageQuotationBadgeCountsAsync(Guid[] ids, CancellationToken ct,
        IReadOnlyDictionary<Guid, int>? requestLegacyCounts = null)
    {
        if (ids.Length == 0) return [];
        var quoteIds = await QuotationBadgeConversationIds(ids).ToArrayAsync(ct);
        if (quoteIds.Length == 0) return [];
        var counts = await PageNotificationUnreadCountsAsync(quoteIds, ct, requestLegacyCounts);
        return quoteIds.ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }

    private IQueryable<Guid> QuotationBadgeConversationIds(Guid[] ids)
    {
        var roots = db.InternalConversations.Where(c => c.CompanyId == user.CompanyId && c.IsActive &&
            c.RelatedType == InternalMailRelatedType.Quotation).Select(c => c.InternalConversationId);
        return Conversations().Where(c => ids.Contains(c.InternalConversationId) &&
            (c.RelatedType == InternalMailRelatedType.Quotation ||
             ((c.RelatedType == InternalMailRelatedType.ConversationTechnical || c.RelatedType == InternalMailRelatedType.ConversationPricing ||
               c.RelatedType == InternalMailRelatedType.ConversationCustom) && c.RelatedId.HasValue && roots.Contains(c.RelatedId.Value))))
            .Select(c => c.InternalConversationId);
    }

    internal IQueryable<NotificationUserState> BadgeStates()
    {
        var suppressed = db.QueryQuotationBadgeSuppressedNotificationIds(user.CompanyId ?? Guid.Empty, user.EmployeeId ?? Guid.Empty);
        return States().Where(s => !suppressed.Contains(s.NotificationId));
    }
}
