using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    internal bool CanSendCompanyMessages => user.IsAuthenticated && user.CompanyId is { } company && company != Guid.Empty &&
        user.EmployeeId is { } employee && employee != Guid.Empty && new CurrentUserPermissionService(user)
            .HasPermission(ApplicationPermissions.InternalMail.SendCompanyMessages);

    internal IQueryable<HRM.Domain.Entities.InternalMailSchema.InternalConversation> SendableConversations()
    {
        var companySender = CanSendCompanyMessages;
        return Conversations().Where(c => companySender || c.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive));
    }

    internal bool CanObserveCompanyConversations => user.IsAuthenticated && user.CompanyId is { } company && company != Guid.Empty &&
        user.EmployeeId is { } employee && employee != Guid.Empty &&
        new CurrentUserPermissionService(user).HasPermission(ApplicationPermissions.InternalMail.ObserveCompanyConversations);
    internal bool CanObserveSampleRequestGroups => ApplicationRoleSets.InternalMail.SampleRequestGroupObservers.Any(user.IsInRole) &&
        new CurrentUserPermissionService(user).HasPermission(ApplicationPermissions.InternalMail.ObserveSampleRequestGroups);

    internal bool CanObservePricing => ApplicationRoleSets.InternalMail.PricingObservers.Any(user.IsInRole) && new CurrentUserPermissionService(user)
        .HasPermission(ApplicationPermissions.InternalMail.ObservePricingGroup);

    internal async Task<IReadOnlyList<Dtos.InternalConversationParticipantDto>> VisibleParticipantsAsync(
        InternalMailRelatedType? type, IReadOnlyList<Dtos.InternalConversationParticipantDto> participants, CancellationToken ct,
        InternalMailParticipantRoles? roleSnapshot = null)
    {
        var visible = participants;
        if (visible.Count == 0) return visible;
        if (!CanObserveCompanyConversations)
        {
            var participantIds = participants.Select(p => p.EmployeeId).ToArray();
            var observers = roleSnapshot?.Developers.ToArray() ?? await RoleEmployees(user.CompanyId ?? Guid.Empty, ApplicationRoleSets.InternalMail.CompanyConversationObservers, activeEmployeesOnly: false)
                .Where(id => participantIds.Contains(id)).ToArrayAsync(ct);
            visible = participants.Where(p => !observers.Contains(p.EmployeeId)).ToArray();
        }
        if (visible.Count == 0) return visible;
        var visibleIds = visible.Select(p => p.EmployeeId).ToArray();
        var leaders = roleSnapshot?.Leaders ?? await RoleEmployees(user.CompanyId ?? Guid.Empty, [ApplicationRoles.Leader])
            .Where(id => visibleIds.Contains(id)).ToHashSetAsync(ct);
        foreach (var participant in visible) participant.IsLeader = leaders.Contains(participant.EmployeeId);
        return visible.OrderByDescending(p => leaders.Contains(p.EmployeeId))
            .ThenBy(p => p.JoinedAt).ThenBy(p => p.EmployeeId).ToArray();
    }

    internal IQueryable<Guid> RoleEmployees(Guid company, IReadOnlyCollection<string> names, bool activeEmployeesOnly = true)
    {
        var normalized = names.Select(name => name.ToUpperInvariant()).ToArray();
        return (from employee in db.Employees
                join account in db.Users on employee.EmployeeId equals account.EmployeeId
                join membership in db.UserRoles on account.Id equals membership.UserId
                join role in db.Roles on membership.RoleId equals role.Id
                where employee.CompanyId == company && (!activeEmployeesOnly || employee.IsActive) && membership.IsActive &&
                    ((role.Name != null && names.Contains(role.Name)) ||
                     (role.NormalizedName != null && normalized.Contains(role.NormalizedName)))
                select employee.EmployeeId).Distinct();
    }

    // GROUP-STORAGE-TRANSITION: legacy-membership-defaults — active rule source until a separately verified Policy cutover.
    // Keep this resolver and QuickGroups preview aligned; policy entities existing does not mean policy is applied.
    // Resolve role defaults on the server; caller-supplied employee IDs never define these grants.
    internal async Task<Guid[]> DefaultMembersAsync(Guid rootId, string area, CancellationToken ct, Guid? workflowCompany = null)
    {
        var company = workflowCompany ?? user.CompanyId ?? Guid.Empty;
        var root = await db.InternalConversations.AsNoTracking().Where(c => c.CompanyId == company &&
                c.IsActive && c.InternalConversationId == rootId && c.RelatedType == InternalMailRelatedType.SampleRequest)
            .Select(c => new { c.RelatedId }).SingleOrDefaultAsync(ct);
        if (root is null) return [];
        IQueryable<Guid> candidates;
        if (area == InternalMailAreas.Pricing)
        {
            var sales = RoleEmployees(company, ApplicationRoleSets.InternalMail.PricingMembers);
            candidates = db.InternalConversationParticipants.Where(p => p.InternalConversationId == rootId && p.IsActive &&
                sales.Contains(p.EmployeeId)).Select(p => p.EmployeeId)
                .Union(RoleEmployees(company, ApplicationRoleSets.InternalMail.PricingExecutives));
            var observers = RoleEmployees(company, ApplicationRoleSets.InternalMail.PricingObservers);
            candidates = candidates.Where(id => !observers.Contains(id));
        }
        else if (area == InternalMailAreas.Technical)
        {
            var category = await db.SampleRequests.Where(s => s.CompanyId == company && s.IsActive && s.SampleRequestId == root.RelatedId &&
                    s.Product.CompanyId == company && s.Product.Category != null && s.Product.Category.CompanyId == company)
                .Select(s => s.Product.Category!.ExternalId).SingleOrDefaultAsync(ct);
            var groupTypes = SampleRequestRecipientRules.ResolveDefaultLeaderGroupTypes(category);
            var leaders = db.MemberInGroups.Where(m => m.IsActive && m.IsAdmin == true && m.Group.CompanyId == company &&
                    m.Group.GroupType != null && groupTypes.Contains(m.Group.GroupType) && m.ProfileNavigation != null &&
                    m.ProfileNavigation.CompanyId == company && m.ProfileNavigation.IsActive)
                .Select(m => m.Profile!.Value);
            candidates = RoleEmployees(company, ApplicationRoleSets.InternalMail.TechnicalMembers).Union(leaders);
        }
        else return [];
        var eligible = Recipients(company, area, rootId);
        return await candidates.Where(id => eligible.Contains(id)).Distinct().ToArrayAsync(ct);
    }

    internal async Task<bool> CanInviteToEmptyGroupAsync(Guid sourceId, Guid[] employeeIds, CancellationToken ct)
    {
        var eligible = Recipients(user.CompanyId ?? Guid.Empty, null, sourceId);
        if (!CanObserveCompanyConversations)
        {
            var hidden = RoleEmployees(user.CompanyId ?? Guid.Empty, ApplicationRoleSets.InternalMail.CompanyConversationObservers, activeEmployeesOnly: false);
            eligible = eligible.Where(id => !hidden.Contains(id) || id == user.EmployeeId);
        }
        var count = await eligible.Where(id => employeeIds.Contains(id)).Distinct().CountAsync(ct);
        return count == employeeIds.Distinct().Count();
    }

    internal async Task<bool> CanForwardToMembersAsync(Guid sourceId, Guid[] employeeIds, CancellationToken ct)
    {
        var source = await Conversations().AsNoTracking().Where(c => c.InternalConversationId == sourceId)
            .Select(c => new { c.RelatedType }).SingleOrDefaultAsync(ct);
        if (source is null) return false;
        // A forwarded copy must never widen the source audience, including the general conversation.
        var eligible = Recipients(user.CompanyId ?? Guid.Empty, AreaOf(source.RelatedType), sourceId);
        var members = await db.InternalConversationParticipants.Where(p => p.InternalConversationId == sourceId && p.IsActive && eligible.Contains(p.EmployeeId) &&
                p.Employee.CompanyId == user.CompanyId && p.Employee.IsActive && employeeIds.Contains(p.EmployeeId))
            .Select(p => p.EmployeeId).ToArrayAsync(ct);
        return employeeIds.All(members.Contains);
    }

    internal async Task<Guid[]> ConstrainDefaultMembersToForwardSourcesAsync(Guid conversationId, Guid[] members, CancellationToken ct)
    {
        var sources = await db.InternalMessageReferences.Where(r => r.Message.InternalConversationId == conversationId &&
            r.RelatedType == InternalMailRelatedType.ConversationForwardSource).Select(r => r.RelatedId).Distinct().ToArrayAsync(ct);
        foreach (var sourceId in sources)
        {
            var source = await db.InternalConversations.AsNoTracking().SingleOrDefaultAsync(c =>
                c.InternalConversationId == sourceId && c.CompanyId == user.CompanyId && c.IsActive, ct);
            if (source is null) return [];
            var eligible = Recipients(user.CompanyId!.Value, AreaOf(source.RelatedType), sourceId);
            members = await db.InternalConversationParticipants.Where(p => p.InternalConversationId == sourceId && p.IsActive &&
                    members.Contains(p.EmployeeId) && eligible.Contains(p.EmployeeId) && p.Employee.CompanyId == user.CompanyId && p.Employee.IsActive)
                .Select(p => p.EmployeeId).Distinct().ToArrayAsync(ct);
        }
        return members;
    }

    // General/executive and legacy pricing readers differ from private-group audiences.
    // Keep restricted copies in technical/custom; sample group observers already see the source.
    internal async Task<bool> IsForwardDestinationBoundedAsync(InternalMailRelatedType? targetType, Guid[] sources, CancellationToken ct)
    {
        if (targetType is InternalMailRelatedType.ConversationCustom or InternalMailRelatedType.ConversationTechnical) return true;
        return !await db.InternalConversations.AnyAsync(c => sources.Contains(c.InternalConversationId) &&
            (c.RelatedType == InternalMailRelatedType.ConversationTechnical || c.RelatedType == InternalMailRelatedType.ConversationPricing ||
             c.RelatedType == InternalMailRelatedType.ConversationCustom), ct);
    }
}
