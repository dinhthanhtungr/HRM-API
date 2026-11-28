using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;

namespace HRM.Application.Features.NotificationHub.Queries.GetItems;

internal static class NotificationHubUrgentQuery
{
    // Inputs retain inbox/company/area visibility. Explicit membership matches the
    // previous conversation-info filter, including for executive read access.
    public static IQueryable<Notification> Apply(
        IQueryable<Notification> visible,
        IQueryable<InternalMailNotificationLink> links,
        IQueryable<InternalConversation> authorizedConversations,
        Guid employeeId)
    {
        return ApplyConversationIds(visible, links, ConversationIds(authorizedConversations, employeeId));
    }

    internal static IQueryable<Guid> ConversationIds(IQueryable<InternalConversation> conversations, Guid employeeId)
        => conversations.Where(c =>
            c.Participants.Any(p => p.EmployeeId == employeeId && p.IsActive) &&
            c.Messages.Any(m => m.InternalConversationId == c.InternalConversationId &&
                !m.IsDeleted && m.IsUrgent && m.SenderEmployeeId != employeeId))
            .Select(c => c.InternalConversationId);

    internal static IQueryable<Notification> ApplyConversationIds(
        IQueryable<Notification> visible, IQueryable<InternalMailNotificationLink> links, IQueryable<Guid> urgentIds)
    {
        // Apply before Take/keyset paging; read-state does not remove urgency.
        return visible.Where(n => links.Any(link => link.NotificationId == n.Id &&
            link.ConversationId.HasValue && urgentIds.Contains(link.ConversationId.Value)));
    }
}
