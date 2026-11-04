using HRM.Application.Commons.Authorization;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    internal IQueryable<Guid> ReactionNoticeDeliveryRecipients(HRM.Domain.Entities.Notifications.Notification notification)
    {
        if (reactionOptions?.Value.Enabled != true) return db.Employees.Where(_ => false).Select(e => e.EmployeeId);
        var company = notification.CompanyId;
        var hidden = RoleEmployees(company, ApplicationRoleSets.InternalMail.CompanyConversationObservers, activeEmployeesOnly: false);
        var messageIds = db.QueryInternalMailNotificationLinks(company).Where(l => l.NotificationId == notification.Id && l.MessageId.HasValue)
            .Select(l => l.MessageId!.Value);
        // Worker has no current-user principal: evaluate privacy relative to the actual author/recipient.
        return db.InternalMessages.Where(m => messageIds.Contains(m.InternalMessageId) && !m.IsDeleted &&
            m.Conversation.CompanyId == company && m.Conversation.IsActive &&
            db.InternalMessageReactions.Any(r => r.MessageId == m.InternalMessageId && r.EmployeeId != m.SenderEmployeeId &&
                r.Employee.CompanyId == company && r.Employee.IsActive &&
                (hidden.Contains(m.SenderEmployeeId) || !hidden.Contains(r.EmployeeId))))
            .Select(m => m.SenderEmployeeId);
    }
    /// <summary>Reaction notices remain visible only to the message author with at least one current visible actor.</summary>
    internal IQueryable<HRM.Domain.Entities.Notifications.Notification> FilterReactionVisibility(
        IQueryable<HRM.Domain.Entities.Notifications.Notification> source)
    {
        var topics = InternalMailPersonalActivity.ReactionTopics;
        if (reactionOptions?.Value.Enabled != true) return source.Where(n => !topics.Contains(n.Topic));
        var company = user.CompanyId ?? Guid.Empty;
        var employee = user.EmployeeId ?? Guid.Empty;
        var visible = Messages().Where(m => !m.IsDeleted && m.SenderEmployeeId == employee).Select(m => m.InternalMessageId);
        var actors = db.InternalMessageReactions.Where(r => r.EmployeeId != employee && r.Employee.CompanyId == company && r.Employee.IsActive);
        if (!CanObserveCompanyConversations)
        {
            var hidden = RoleEmployees(company, ApplicationRoleSets.InternalMail.CompanyConversationObservers, activeEmployeesOnly: false);
            actors = actors.Where(r => !hidden.Contains(r.EmployeeId));
        }
        var links = db.QueryInternalMailNotificationLinks(company);
        return source.Where(n => !topics.Contains(n.Topic) || links.Any(l => l.NotificationId == n.Id && l.MessageId.HasValue &&
            visible.Contains(l.MessageId.Value) && actors.Any(r => r.MessageId == l.MessageId.Value)));
    }

    internal IQueryable<HRM.Application.Abstractions.Persistence.InternalMail.InternalConversationNotificationUnreadCount> PersonalReactionUnreadCounts()
    {
        var topics = InternalMailPersonalActivity.ReactionTopics;
        var states = States().Where(s => !s.IsRead && !s.IsArchived && topics.Contains(s.Notification.Topic));
        var links = db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty);
        return from state in states
               join link in links on state.NotificationId equals link.NotificationId
               where link.ConversationId.HasValue
               group link by link.ConversationId!.Value into g
               select new HRM.Application.Abstractions.Persistence.InternalMail.InternalConversationNotificationUnreadCount
               { ConversationId = g.Key, UnreadCount = g.Count() };
    }
    /// <summary>Dispatch rechecks active membership and observer scope; mute does not suppress cache invalidation.</summary>
    public IQueryable<Guid> ReactionReaders(Guid company, Guid conversationId)
    {
        var developers = RoleEmployees(company, ApplicationRoleSets.InternalMail.CompanyConversationObservers);
        var colors = ColorConversationIds(company);
        var rd = RdLeaderIds(company);
        var validGroups = GroupScopes(company).Select(g => g.ConversationId);
        return db.Employees.Where(e => e.CompanyId == company && e.IsActive &&
            (developers.Contains(e.EmployeeId) || !colors.Contains(conversationId) || !rd.Contains(e.EmployeeId)) &&
            db.InternalConversations.Any(c => c.InternalConversationId == conversationId && c.CompanyId == company && c.IsActive &&
                (!UsesGroupStorage || validGroups.Contains(c.InternalConversationId)) &&
                ((c.RelatedType != InternalMailRelatedType.ConversationTechnical && c.RelatedType != InternalMailRelatedType.ConversationPricing &&
                  c.RelatedType != InternalMailRelatedType.ConversationCustom) || db.InternalConversations.Any(root => root.InternalConversationId == c.RelatedId &&
                    root.CompanyId == company && root.IsActive)) &&
                (developers.Contains(e.EmployeeId) || c.Participants.Any(p => p.EmployeeId == e.EmployeeId && p.IsActive))))
            .Select(e => e.EmployeeId);
    }
}
