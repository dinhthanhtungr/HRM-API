using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Domain.Entities.Notifications;

namespace HRM.Application.Features.NotificationHub.Queries.GetItems;

internal static class NotificationHubUnreadQuery
{
    public static IQueryable<Notification> ApplyConversationScopes(IQueryable<Notification> visible,
        IQueryable<Notification> inbox, IQueryable<InternalMailNotificationLink> links,
        Guid[] unreadConversationIds, Guid employeeId)
    {
        var ownUnreadIds = inbox.Where(n => n.UserStates.Any(s =>
            s.UserId == employeeId && !s.IsRead && !s.IsArchived)).Select(n => n.Id);
        // Standalone notifications remain an SQL predicate; no historical notification-ID array.
        return visible.Where(n =>
            (ownUnreadIds.Contains(n.Id) && !links.Any(l => l.NotificationId == n.Id && l.HasConversation)) ||
            links.Any(l => l.NotificationId == n.Id && l.ConversationId.HasValue &&
                unreadConversationIds.Contains(l.ConversationId.Value)));
    }

    // Inputs retain the caller's company/inbox/visibility scope. Filter before keyset paging.
    public static IQueryable<Notification> Apply(IQueryable<Notification> visible,
        IQueryable<Notification> inbox, IQueryable<InternalMailNotificationLink> links,
        IQueryable<Guid> unreadMessageConversationIds, Guid employeeId)
    {
        var unreadNotificationIds = inbox.Where(n => n.UserStates.Any(s =>
            s.UserId == employeeId && !s.IsRead && !s.IsArchived)).Select(n => n.Id);
        var unreadConversationIds = links.Where(l => l.ConversationId.HasValue &&
            unreadNotificationIds.Contains(l.NotificationId)).Select(l => l.ConversationId!.Value);
        // The latest event can already be read while an older event/message remains unread.
        return visible.Where(n => unreadNotificationIds.Contains(n.Id) || links.Any(l =>
            l.NotificationId == n.Id && l.ConversationId.HasValue &&
            (unreadConversationIds.Contains(l.ConversationId.Value) ||
             unreadMessageConversationIds.Contains(l.ConversationId.Value))));
    }
}
