using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    /// <summary>Read-only, page-batched navigation and unread counts. No default-group writes.</summary>
    internal async Task<IReadOnlyDictionary<Guid, IReadOnlyList<InternalMailQuickGroupDto>>> QuickGroupsAsync(
        IEnumerable<Guid> sourceRootIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, IReadOnlyList<InternalMailQuickGroupDto>>();
        var ids = sourceRootIds.Distinct().ToArray();
        if (ids.Length == 0 || !user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is not { } employee)
            return result;
        var visible = Conversations();
        var visibleGroups = VisibleGroupScopes();
        var colors = ColorRoots(company);
        var roots = await db.InternalConversations.AsNoTracking().Where(c => ids.Contains(c.InternalConversationId) &&
                c.CompanyId == company && c.IsActive && c.RelatedType == InternalMailRelatedType.SampleRequest &&
                visibleGroups.Any(g => g.RootConversationId == c.InternalConversationId))
            .Select(c => new
            {
                Id = c.InternalConversationId,
                CanReadRoot = visible.Any(v => v.InternalConversationId == c.InternalConversationId),
                IsMember = c.Participants.Any(p => p.EmployeeId == employee && p.IsActive),
                IsColor = colors.Contains(c.InternalConversationId),
                Category = db.SampleRequests.Where(s => s.CompanyId == company && s.IsActive && s.SampleRequestId == c.RelatedId &&
                        s.Product.CompanyId == company && s.Product.Category != null && s.Product.Category.CompanyId == company)
                    .Select(s => s.Product.Category!.ExternalId).FirstOrDefault()
            }).ToListAsync(ct);
        if (roots.Count == 0) return result;
        var rootIds = roots.Select(r => r.Id).ToArray();
        // Include inaccessible children internally to avoid offering a replacement for a removed membership.
        var children = await GroupScopes(company).AsNoTracking().Where(g => rootIds.Contains(g.RootConversationId) &&
                (g.GroupType == InternalConversationGroupType.Technical || g.GroupType == InternalConversationGroupType.Pricing))
            .Select(g => new { Id = g.ConversationId, RootId = g.RootConversationId, g.Conversation.RelatedType,
                Visible = visible.Any(v => v.InternalConversationId == g.ConversationId) }).ToListAsync(ct);
        var needsPreview = roots.Any(r => r.CanReadRoot && children.Count(c => c.RootId == r.Id) < 2);
        var visibleChildIds = children.Where(c => c.Visible).Select(c => c.Id).ToArray();
        var unreadCounts = new Dictionary<Guid, int>();
        if (visibleChildIds.Length > 0)
        {
            var legacyCounts = await Conversations().AsNoTracking()
                .Where(c => visibleChildIds.Contains(c.InternalConversationId))
                .Select(c => new { Id = c.InternalConversationId,
                    Count = c.Messages.Count(m => !m.IsDeleted && m.SenderEmployeeId != employee &&
                        (m.PayloadJson == null || !EF.Functions.JsonContains(m.PayloadJson, InternalMailMentions.AudienceMarker)) &&
                        m.ReadStates.Any(s => s.EmployeeId == employee && !s.IsRead)) })
                .ToListAsync(ct);
            var notificationCounts = await NotificationUnreadCounts(visibleChildIds)
                .ToDictionaryAsync(c => c.ConversationId, c => c.UnreadCount, ct);
            foreach (var count in legacyCounts)
                unreadCounts[count.Id] = Math.Max(count.Count, notificationCounts.GetValueOrDefault(count.Id));
        }
        var observer = CanObserveSampleRequestGroups;
        var pricingObserver = CanObservePricing;
        // GROUP-STORAGE-TRANSITION: legacy-membership-defaults — preview mirrors the current role/default-member resolver.
        // Replace together with DefaultMembersAsync after Policy parity checks; keep visibility/revocation checks.
        var sales = false;
        var executive = false;
        var technical = false;
        var active = false;
        var rd = false;
        string[] leaderTypes = [];
        if (needsPreview && !observer)
        {
            var saleIds = RoleEmployees(company, ApplicationRoleSets.InternalMail.PricingMembers);
            var executiveIds = RoleEmployees(company, ApplicationRoleSets.InternalMail.PricingExecutives);
            var technicalIds = RoleEmployees(company, ApplicationRoleSets.InternalMail.TechnicalMembers);
            var hiddenIds = RoleEmployees(company, ApplicationRoleSets.InternalMail.PricingObservers);
            var rdIds = RdLeaderIds(company);
            var roles = await db.Employees.AsNoTracking().Where(e => e.EmployeeId == employee && e.CompanyId == company && e.IsActive)
                .Select(e => new { Sale = saleIds.Contains(e.EmployeeId) && !hiddenIds.Contains(e.EmployeeId),
                    Executive = executiveIds.Contains(e.EmployeeId) && !hiddenIds.Contains(e.EmployeeId),
                    Technical = technicalIds.Contains(e.EmployeeId), Rd = rdIds.Contains(e.EmployeeId) }).SingleOrDefaultAsync(ct);
            active = roles != null;
            sales = roles?.Sale == true;
            executive = roles?.Executive == true;
            technical = roles?.Technical == true;
            rd = roles?.Rd == true;
            leaderTypes = await db.MemberInGroups.AsNoTracking().Where(m => m.Profile == employee && m.IsActive && m.IsAdmin == true &&
                    m.Group.CompanyId == company && m.Group.GroupType != null && m.ProfileNavigation != null &&
                    m.ProfileNavigation.CompanyId == company && m.ProfileNavigation.IsActive)
                .Select(m => m.Group.GroupType!).Distinct().ToArrayAsync(ct);
        }
        foreach (var root in roots)
        {
            var groups = new List<InternalMailQuickGroupDto>();
            foreach (var type in new[] { InternalMailRelatedType.ConversationTechnical, InternalMailRelatedType.ConversationPricing })
            {
                var child = children.FirstOrDefault(c => c.RootId == root.Id && c.RelatedType == type);
                var code = AreaOf(type);
                if (child != null)
                {
                    if (child.Visible) groups.Add(new(code, child.Id, unreadCounts.GetValueOrDefault(child.Id)));
                    continue;
                }
                if (!root.CanReadRoot) continue;
                var eligible = active && (!root.IsColor || !rd);
                var defaultMember = type == InternalMailRelatedType.ConversationPricing
                    ? executive || (sales && root.IsMember)
                    : technical || SampleRequestRecipientRules.ResolveDefaultLeaderGroupTypes(root.Category).Any(leaderTypes.Contains);
                if (observer || (type == InternalMailRelatedType.ConversationPricing && pricingObserver) || (eligible && defaultMember))
                    groups.Add(new(code, null));
            }
            result[root.Id] = groups;
        }
        return result;
    }
}
