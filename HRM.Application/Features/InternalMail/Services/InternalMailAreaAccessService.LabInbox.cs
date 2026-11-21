using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    internal sealed record InboxNotificationReadScope(IQueryable<Notification> Query, Guid[]? AuthorizedEventIds,
        Guid CompanyId, Guid EmployeeId, Guid UserId);

    internal async Task<IQueryable<Notification>> DefaultLabInboxNotificationsAsync(
        IQueryable<Notification> source, CancellationToken ct, bool excludeReactions = false,
        Guid[]? candidateConversationIds = null, Guid[]? candidateEventIds = null)
        => (await DefaultLabInboxSelectionAsync(source, ct, excludeReactions, candidateConversationIds, candidateEventIds)).Query;

    internal async Task<InboxNotificationReadScope> DefaultLabInboxSelectionAsync(
        IQueryable<Notification> source, CancellationToken ct, bool excludeReactions = false,
        Guid[]? candidateConversationIds = null, Guid[]? candidateEventIds = null)
    {
        var authorized = ConversationsForCandidates(candidateConversationIds);
        if (CanObserveCompanyConversations)
            return await ScopeDefaultInboxNotificationsAsync(source, authorized.Select(c => c.InternalConversationId),
                excludeReactions, candidateEventIds, ct);
        var company = user.CompanyId ?? Guid.Empty;
        var teams = await db.MemberInGroups.AsNoTracking().Where(m => m.IsActive && m.Profile == user.EmployeeId &&
            m.Group.CompanyId == company && m.ProfileNavigation != null &&
            m.ProfileNavigation.IsActive && m.ProfileNavigation.CompanyId == company &&
            (m.Group.GroupType == SampleRequestLabTeamRules.Rd || m.Group.GroupType == SampleRequestLabTeamRules.Color))
            .Select(m => m.Group.GroupType).Distinct().ToArrayAsync(ct);
        if (teams.Length == 0)
            return await ScopeDefaultInboxNotificationsAsync(source, authorized.Select(c => c.InternalConversationId),
                excludeReactions, candidateEventIds, ct);
        var categories = (teams.Contains(SampleRequestLabTeamRules.Rd) ? SampleRequestLabTeamRules.RdCategories : [])
            .Concat(teams.Contains(SampleRequestLabTeamRules.Color) ? SampleRequestLabTeamRules.ColorCategories : [])
            .Distinct().ToArray();
        var samples = db.SampleRequests.Where(s => s.CompanyId == company && s.IsActive &&
            s.Product.CompanyId == company && s.Product.Category != null && s.Product.Category.CompanyId == company &&
            s.Product.Category.ExternalId != null && categories.Contains(s.Product.Category.ExternalId.Trim().ToUpper()))
            .Select(s => s.SampleRequestId);
        var roots = db.InternalConversations.Where(c => c.CompanyId == company && c.IsActive &&
            c.RelatedType == InternalMailRelatedType.SampleRequest);
        if (candidateConversationIds is not null)
        {
            var parents = await db.InternalConversations.Where(c => c.CompanyId == company &&
                    candidateConversationIds.Contains(c.InternalConversationId) && c.RelatedId.HasValue)
                .Select(c => c.RelatedId!.Value).Distinct().ToArrayAsync(ct);
            var rootIds = candidateConversationIds.Concat(parents).Distinct().ToArray();
            roots = roots.Where(c => rootIds.Contains(c.InternalConversationId));
        }
        var matchingRoots = roots.Where(c => c.RelatedId.HasValue && samples.Contains(c.RelatedId.Value))
            .Select(c => c.InternalConversationId);
        // Apply presentation scope inside the authorized conversation set. Resolve
        // JSON notification links only once, instead of intersecting two link scans.
        var ids = authorized.Where(c => c.RelatedType == InternalMailRelatedType.SampleRequest
            ? matchingRoots.Contains(c.InternalConversationId)
            : !roots.Any(r => r.InternalConversationId == c.RelatedId) || matchingRoots.Contains(c.RelatedId!.Value))
            .Select(c => c.InternalConversationId);
        return await ScopeDefaultInboxNotificationsAsync(source, ids, excludeReactions, candidateEventIds, ct);
    }

    private async Task<InboxNotificationReadScope> ScopeDefaultInboxNotificationsAsync(
        IQueryable<Notification> source, IQueryable<Guid> authorizedIds, bool excludeReactions,
        Guid[]? candidateEventIds, CancellationToken ct)
    {
        Guid[]? reactionMessages = null;
        if (candidateEventIds is not null && !excludeReactions && reactionOptions?.Value.Enabled == true)
        {
            var topics = InternalMailPersonalActivity.ReactionTopics;
            var reactions = source.Where(n => candidateEventIds.Contains(n.Id) && topics.Contains(n.Topic)).Select(n => n.Id);
            var targets = await db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty)
                .Where(l => l.MessageId.HasValue && reactions.Contains(l.NotificationId))
                .Select(l => l.MessageId!.Value).Distinct().ToArrayAsync(ct);
            reactionMessages = await AuthorizedOwnReactionMessageIdsAsync(targets, ct);
        }
        var scoped = NotificationsForConversations(source, authorizedIds, excludeReactions, candidateEventIds, reactionMessages);
        var company = user.CompanyId ?? Guid.Empty;
        var employee = user.EmployeeId ?? Guid.Empty;
        if (candidateEventIds is null) return new(scoped, null, company, employee, user.UserId);
        // Resolve every notification/reaction gate once for this complete bounded
        // invocation. Only keys are retained, never data or a cross-request grant.
        var ids = await scoped.Select(n => n.Id).ToArrayAsync(ct);
        return new(source.Where(n => n.CompanyId == company && ids.Contains(n.Id)), ids, company, employee, user.UserId);
    }

    internal IQueryable<Guid> LabMemberIds(Guid company) => db.MemberInGroups.Where(m => m.IsActive &&
        m.Group.CompanyId == company && m.Profile.HasValue && m.ProfileNavigation != null &&
        m.ProfileNavigation.IsActive && m.ProfileNavigation.CompanyId == company &&
        (m.Group.GroupType == SampleRequestLabTeamRules.Rd || m.Group.GroupType == SampleRequestLabTeamRules.Color))
        .Select(m => m.Profile!.Value).Distinct();

    // Presentation scope only: callers must retain Conversations()/Notifications() authorization.
    internal IQueryable<InternalConversation> DefaultLabInboxConversations()
    {
        var company = user.CompanyId ?? Guid.Empty;
        var teams = db.MemberInGroups.Where(m => m.IsActive && m.Profile == user.EmployeeId &&
            m.Group.CompanyId == company && m.ProfileNavigation != null &&
            m.ProfileNavigation.IsActive && m.ProfileNavigation.CompanyId == company &&
            (m.Group.GroupType == SampleRequestLabTeamRules.Rd || m.Group.GroupType == SampleRequestLabTeamRules.Color))
            .Select(m => m.Group.GroupType);
        var samples = db.SampleRequests.Where(s => s.CompanyId == company && s.IsActive &&
            s.Product.CompanyId == company && s.Product.Category != null && s.Product.Category.CompanyId == company &&
            s.Product.Category.ExternalId != null &&
            ((teams.Contains(SampleRequestLabTeamRules.Rd) && SampleRequestLabTeamRules.RdCategories.Contains(s.Product.Category.ExternalId.Trim().ToUpper())) ||
             (teams.Contains(SampleRequestLabTeamRules.Color) && SampleRequestLabTeamRules.ColorCategories.Contains(s.Product.Category.ExternalId.Trim().ToUpper()))))
            .Select(s => s.SampleRequestId);
        var roots = db.InternalConversations.Where(c => c.CompanyId == company && c.IsActive &&
            c.RelatedType == InternalMailRelatedType.SampleRequest);
        var matchingRoots = roots.Where(c => c.RelatedId.HasValue && samples.Contains(c.RelatedId.Value))
            .Select(c => c.InternalConversationId);
        return db.InternalConversations.Where(c => c.CompanyId == company &&
            (CanObserveCompanyConversations || !teams.Any() ||
             (c.RelatedType == InternalMailRelatedType.SampleRequest
                ? matchingRoots.Contains(c.InternalConversationId)
                : !roots.Any(r => r.InternalConversationId == c.RelatedId) ||
                  matchingRoots.Contains(c.RelatedId!.Value))));
    }

    internal IQueryable<Notification> DefaultLabInboxNotifications(IQueryable<Notification> source)
    {
        var visibleIds = DefaultLabInboxConversations().Select(c => c.InternalConversationId);
        var links = db.QueryInternalMailNotificationLinks(user.CompanyId ?? Guid.Empty);
        return source.Where(n => links.Any(l => l.NotificationId == n.Id &&
            (!l.HasConversation || (l.ConversationId.HasValue && visibleIds.Contains(l.ConversationId.Value)))));
    }
}
