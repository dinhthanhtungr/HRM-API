using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;
public sealed partial class InternalConversationGroupStoragePostgresTests
{
    // Frozen reference before the request-local visibility batching optimization.
    /// <summary>Read-only, page-batched navigation and unread counts. No default-group writes.</summary>
    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<InternalMailQuickGroupDto>>> ReferenceQuickGroupsAsync(ApplicationDbContext db, InternalMailAreaAccessService access, UnreadPerformanceUser user,
        IEnumerable<Guid> sourceRootIds, CancellationToken ct,
        IReadOnlyDictionary<Guid, int>? pageUnreadCounts = null)
    {
        var result = new Dictionary<Guid, IReadOnlyList<InternalMailQuickGroupDto>>();
        var ids = sourceRootIds.Distinct().ToArray();
        if (ids.Length == 0 || !user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is not { } employee)
            return result;
        var visible = access.Conversations();
        var visibleGroups = access.VisibleGroupScopes();
        var colors = ReferenceColorRoots(db, company);
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
        var children = await access.GroupScopes(company).AsNoTracking().Where(g => rootIds.Contains(g.RootConversationId) &&
                (g.GroupType == InternalConversationGroupType.Technical || g.GroupType == InternalConversationGroupType.Pricing))
            .Select(g => new { Id = g.ConversationId, RootId = g.RootConversationId, g.Conversation.RelatedType,
                Visible = visible.Any(v => v.InternalConversationId == g.ConversationId) }).ToListAsync(ct);
        var needsPreview = roots.Any(r => r.CanReadRoot && children.Count(c => c.RootId == r.Id) < 2);
        var visibleChildIds = children.Where(c => c.Visible).Select(c => c.Id).ToArray();
        var unreadCounts = new Dictionary<Guid, int>();
        foreach (var id in visibleChildIds)
            if (pageUnreadCounts != null && pageUnreadCounts.TryGetValue(id, out var count))
                unreadCounts[id] = count;
        // Reuse only canonical counts from this request; absent entries still need a scoped count.
        var missingCountIds = visibleChildIds.Where(id => !unreadCounts.ContainsKey(id)).ToArray();
        if (missingCountIds.Length > 0)
        {
            var missingCounts = await access.NotificationUnreadCounts(missingCountIds)
                .ToDictionaryAsync(c => c.ConversationId, c => c.UnreadCount, ct);
            foreach (var entry in missingCounts) unreadCounts[entry.Key] = entry.Value;
        }
        var observer = access.CanObserveSampleRequestGroups;
        var pricingObserver = access.CanObservePricing;
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
            var saleIds = access.RoleEmployees(company, ApplicationRoleSets.InternalMail.PricingMembers);
            var executiveIds = access.RoleEmployees(company, ApplicationRoleSets.InternalMail.PricingExecutives);
            var technicalIds = access.RoleEmployees(company, ApplicationRoleSets.InternalMail.TechnicalMembers);
            var hiddenIds = access.RoleEmployees(company, ApplicationRoleSets.InternalMail.PricingObservers);
            var rdIds = access.RdLeaderIds(company);
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
                var code = InternalMailAreaAccessService.AreaOf(type);
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
    private static IQueryable<Guid> ReferenceColorRoots(ApplicationDbContext db, Guid company) => db.InternalConversations.Where(c => c.CompanyId == company &&
        ((c.RelatedType == InternalMailRelatedType.SampleRequest && db.SampleRequests.Any(s =>
            s.CompanyId == company && s.SampleRequestId == c.RelatedId && s.Product.CompanyId == company &&
            s.Product.Category != null && s.Product.Category.CompanyId == company &&
            new[] { "PIG", "CMB", "PHM", "PBM", "PDM" }.Contains(s.Product.Category.ExternalId!.Trim().ToUpper()))) ||
         (c.RelatedType == InternalMailRelatedType.Quotation && db.Quotations.Any(q =>
            q.CompanyId == company && q.QuotationId == c.RelatedId && q.Lines.Any(l => l.IsActive &&
                l.ProductNavigation.CompanyId == company && l.ProductNavigation.Category != null &&
                l.ProductNavigation.Category.CompanyId == company &&
                new[] { "PIG", "CMB", "PHM", "PBM", "PDM" }.Contains(l.ProductNavigation.Category.ExternalId!.Trim().ToUpper()))))))
        .Select(c => c.InternalConversationId);

}
