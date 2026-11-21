using HRM.Application.Commons.Authorization;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    internal async Task<int> PersonalReactionUnreadCountAsync(CancellationToken cancellationToken)
    {
        if (reactionOptions?.Value.Enabled != true) return 0;
        var company = user.CompanyId ?? Guid.Empty;
        var employee = user.EmployeeId ?? Guid.Empty;
        var topics = InternalMailPersonalActivity.ReactionTopics;
        // Shortlist only inbox keys before expanding JSON links and conversation/actor visibility.
        // Do not cache: the final query rechecks read/archive state and all current permissions.
        var candidates = await db.NotificationUserStates.AsNoTracking().Where(s =>
            s.UserId == employee && !s.IsRead && !s.IsArchived && s.Notification.CompanyId == company &&
            topics.Contains(s.Notification.Topic)).Select(s => s.NotificationId).Distinct().ToArrayAsync(cancellationToken);
        var count = 0;
        foreach (var batch in candidates.Chunk(256))
        {
            var visible = Notifications(db.Notifications.Where(n => batch.Contains(n.Id))).Select(n => n.Id);
            count += await db.NotificationUserStates.CountAsync(s => s.UserId == employee && !s.IsRead &&
                !s.IsArchived && batch.Contains(s.NotificationId) && visible.Contains(s.NotificationId), cancellationToken);
        }
        return count;
    }

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
        IQueryable<HRM.Domain.Entities.Notifications.Notification> source) => FilterReactionVisibility(source, null);

    private IQueryable<HRM.Domain.Entities.Notifications.Notification> FilterReactionVisibility(
        IQueryable<HRM.Domain.Entities.Notifications.Notification> source, Guid[]? authorizedMessageIds)
    {
        var topics = InternalMailPersonalActivity.ReactionTopics;
        if (reactionOptions?.Value.Enabled != true) return source.Where(n => !topics.Contains(n.Topic));
        var company = user.CompanyId ?? Guid.Empty;
        var employee = user.EmployeeId ?? Guid.Empty;
        var messages = authorizedMessageIds is null ? Messages() : db.InternalMessages.Where(m =>
            authorizedMessageIds.Contains(m.InternalMessageId) && m.Conversation.CompanyId == company && m.Conversation.IsActive);
        var visible = messages.Where(m => !m.IsDeleted && m.SenderEmployeeId == employee).Select(m => m.InternalMessageId);
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

    // Input keys are a shortlist, not a grant. Resolve the message's actual
    // conversation rather than trusting the notification's conversation link.
    internal async Task<Guid[]> AuthorizedOwnReactionMessageIdsAsync(Guid[] messageIds, CancellationToken ct)
    {
        if (messageIds.Length == 0) return [];
        var targets = db.InternalMessages.Where(m => messageIds.Contains(m.InternalMessageId));
        var conversationIds = await targets.Select(m => m.InternalConversationId).Distinct().ToArrayAsync(ct);
        var visible = ConversationsForCandidates(conversationIds).Select(c => c.InternalConversationId);
        return await targets.Where(m => !m.IsDeleted && m.SenderEmployeeId == user.EmployeeId &&
            visible.Contains(m.InternalConversationId)).Select(m => m.InternalMessageId).ToArrayAsync(ct);
    }

    internal IQueryable<HRM.Application.Abstractions.Persistence.InternalMail.InternalConversationNotificationUnreadCount> PersonalReactionUnreadCounts(
        InboxNotificationReadScope? readScope = null)
    {
        var topics = InternalMailPersonalActivity.ReactionTopics;
        var authorizedEventIds = readScope is not null && readScope.UserId == user.UserId &&
            readScope.CompanyId == user.CompanyId && readScope.EmployeeId == user.EmployeeId
            ? readScope.AuthorizedEventIds : null;
        var source = authorizedEventIds is null ? States() : db.NotificationUserStates.Where(s =>
            s.UserId == user.EmployeeId && s.Notification.CompanyId == user.CompanyId && authorizedEventIds.Contains(s.NotificationId));
        var states = source.Where(s => !s.IsRead && !s.IsArchived && topics.Contains(s.Notification.Topic));
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
