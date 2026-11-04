using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.DefaultMembership;

/// <summary>Company-scoped configuration only. No method writes participants, messages, read states or notifications.</summary>
public sealed partial class DefaultMembershipPolicyService(IInternalMailDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IDateTimeProvider clock)
{
    private bool Authorized => user.IsAuthenticated && user.CompanyId is { } company && company != Guid.Empty &&
        user.EmployeeId is { } employee && employee != Guid.Empty &&
        permissions.HasPermission(ApplicationPermissions.InternalMail.ManageDefaultMembershipPolicies);

    public async Task<MembershipRequestResult<IReadOnlyList<DefaultMembershipPolicyDto>>> PoliciesAsync(CancellationToken ct)
    {
        if (!Authorized) return new(MembershipRequestStatus.Forbidden);
        var policies = await db.DefaultMembershipPolicies.AsNoTracking().Where(p => p.CompanyId == user.CompanyId)
            .Include(p => p.ActiveRevision).ThenInclude(r => r!.Rules).ToListAsync(ct);
        var result = new[] { InternalConversationGroupType.Technical, InternalConversationGroupType.Pricing }
            .Select(type => policies.SingleOrDefault(p => p.GroupType == type) is { } policy ? Dto(policy) :
                new DefaultMembershipPolicyDto(null, type, 0, null, false, [])).ToArray();
        return new(MembershipRequestStatus.Success, result);
    }

    public async Task<MembershipRequestResult<IReadOnlyList<DefaultMembershipRevisionDto>>> RevisionsAsync(
        InternalConversationGroupType type, int take, int? beforeVersion, CancellationToken ct)
    {
        if (!Authorized) return new(MembershipRequestStatus.Forbidden);
        if (!DefaultMembershipRuleValidation.ValidKind(type) || take is < 1 or > 100 || beforeVersion is <= 0)
            return new(MembershipRequestStatus.Invalid, ErrorCode: "InvalidHistoryQuery");
        var policy = await db.DefaultMembershipPolicies.AsNoTracking().SingleOrDefaultAsync(p => p.CompanyId == user.CompanyId && p.GroupType == type, ct);
        if (policy is null) return new(MembershipRequestStatus.Success, []);
        var revisions = await db.DefaultMembershipRevisions.AsNoTracking().Where(r => r.CompanyId == user.CompanyId &&
                r.PolicyId == policy.PolicyId && (!beforeVersion.HasValue || r.Version < beforeVersion.Value))
            .Include(r => r.Rules).OrderByDescending(r => r.Version).Take(take).ToListAsync(ct);
        return new(MembershipRequestStatus.Success, revisions.Select(r => new DefaultMembershipRevisionDto(r.RevisionId, r.Version,
            r.CreatedBy, r.CreatedAt, r.Reason, r.RestoredFromRevisionId, policy.ActiveRevisionId == r.RevisionId,
            r.Rules.OrderBy(rule => rule.RuleId).Select(PublishedRuleDto).ToArray())).ToArray());
    }

    public async Task<MembershipRequestResult<DefaultMembershipOptions>> OptionsAsync(CancellationToken ct)
    {
        if (!Authorized) return new(MembershipRequestStatus.Forbidden);
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => new MembershipOption(r.Id, r.NormalizedName, r.Name)).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().Where(c => c.CompanyId == user.CompanyId && c.IsActive != false)
            .OrderBy(c => c.ExternalId).Select(c => new MembershipOption(c.CategoryId, c.ExternalId, c.Name)).ToListAsync(ct);
        var codes = await db.Groups.AsNoTracking().Where(g => g.CompanyId == user.CompanyId && g.GroupType != null && g.GroupType != "")
            .Select(g => g.GroupType!.Trim().ToUpper()).Distinct().OrderBy(g => g).ToListAsync(ct);
        return new(MembershipRequestStatus.Success, new(roles, categories, codes));
    }

    private static DefaultMembershipRuleDto RuleDto(DefaultMembershipRule r) => new(r.SourceType, r.RoleId, r.GroupTypeCode, r.CategoryScope, r.ProductCategoryId);
    private static PublishedDefaultMembershipRuleDto PublishedRuleDto(DefaultMembershipRule r) => new(r.RuleId, r.SourceType, r.RoleId, r.GroupTypeCode, r.CategoryScope, r.ProductCategoryId);
    private static DefaultMembershipPolicyDto Dto(DefaultMembershipPolicy p) => new(p.PolicyId, p.GroupType, p.Version, p.ActiveRevisionId,
        p.ActiveRevisionId.HasValue, p.ActiveRevision?.Rules.OrderBy(r => r.RuleId).Select(PublishedRuleDto).ToArray() ?? []);
}
