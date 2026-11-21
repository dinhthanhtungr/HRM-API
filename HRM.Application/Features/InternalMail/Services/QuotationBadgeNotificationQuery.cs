using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
namespace HRM.Application.Features.InternalMail.Services;

internal static class QuotationBadgeNotificationQuery
{
    // Provider-neutral reference; PostgreSQL implements the same policy with one window query.
    internal static IQueryable<Guid> Suppressed(IQueryable<InternalConversation> conversations,
        IQueryable<InternalMessage> messages, IQueryable<Notification> notifications, IQueryable<NotificationUserState> states,
        IQueryable<InternalMailNotificationLink> links, Guid company, Guid employee)
    {
        var roots = conversations.Where(c => c.CompanyId == company && c.IsActive && c.RelatedType == InternalMailRelatedType.Quotation)
            .Select(c => c.InternalConversationId);
        var quoteIds = conversations.Where(c => c.CompanyId == company && c.IsActive &&
            (roots.Contains(c.InternalConversationId) ||
             ((c.RelatedType == InternalMailRelatedType.ConversationTechnical || c.RelatedType == InternalMailRelatedType.ConversationPricing ||
               c.RelatedType == InternalMailRelatedType.ConversationCustom) && c.RelatedId.HasValue && roots.Contains(c.RelatedId.Value))))
            .Select(c => c.InternalConversationId);
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        var unread = notifications.Where(n => n.CompanyId == company && !reactions.Contains(n.Topic) &&
            states.Any(s => s.NotificationId == n.Id && s.UserId == employee && !s.IsRead && !s.IsArchived));
        var rows = from link in links
                   join n in unread on link.NotificationId equals n.Id
                   where link.ConversationId.HasValue && quoteIds.Contains(link.ConversationId.Value)
                   select new { Link=link, Notification=n };
        var valid = rows.Where(row => row.Link.MessageId.HasValue && messages.Any(m => m.InternalMessageId == row.Link.MessageId &&
            m.InternalConversationId == row.Link.ConversationId && !m.IsDeleted && m.SenderEmployeeId != employee));
        return rows.Where(row => !valid.Any(v => v.Notification.Id == row.Notification.Id) || valid.Any(newer =>
            newer.Link.MessageId == row.Link.MessageId && newer.Link.ConversationId == row.Link.ConversationId &&
            (newer.Notification.CreatedDate > row.Notification.CreatedDate ||
             (newer.Notification.CreatedDate == row.Notification.CreatedDate && newer.Notification.Id.CompareTo(row.Notification.Id)>0))))
            .Select(row => row.Notification.Id);
    }
}
