using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.DefaultMembership;

public sealed partial class DefaultMembershipPolicyService
{
    public Task<MembershipRequestResult<DefaultMembershipPolicyDto>> PublishAsync(InternalConversationGroupType type,
        PublishDefaultMembershipRequest request, CancellationToken ct) =>
        PublishCoreAsync(type, request.ExpectedVersion, request.Reason, request.Rules, null, ct);

    public Task<MembershipRequestResult<DefaultMembershipPolicyDto>> RollbackAsync(InternalConversationGroupType type,
        RollbackDefaultMembershipRequest request, CancellationToken ct) =>
        PublishCoreAsync(type, request.ExpectedVersion, request.Reason, null, request.RevisionId, ct);

    private async Task<MembershipRequestResult<DefaultMembershipPolicyDto>> PublishCoreAsync(InternalConversationGroupType type,
        int? expected, string? reason, IReadOnlyList<DefaultMembershipRuleDto>? input, Guid? restoreId, CancellationToken ct)
    {
        if (!Authorized) return new(MembershipRequestStatus.Forbidden);
        if (!DefaultMembershipRuleValidation.ValidKind(type) || expected is null or < 0 or int.MaxValue ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500 || restoreId == Guid.Empty)
            return new(MembershipRequestStatus.Invalid, ErrorCode: "InvalidPublicationRequest");
        await using var transaction = await db.BeginTransactionAsync(ct);
        await db.LockDefaultMembershipPolicyAsync(user.CompanyId!.Value, type, ct);
        var policy = await db.DefaultMembershipPolicies.Include(p => p.ActiveRevision).ThenInclude(r => r!.Rules)
            .SingleOrDefaultAsync(p => p.CompanyId == user.CompanyId && p.GroupType == type, ct);
        if ((policy?.Version ?? 0) != expected.Value)
            return new(MembershipRequestStatus.Conflict, ErrorCode: "ExpectedVersionMismatch");
        if (restoreId.HasValue)
        {
            if (policy is null) return new(MembershipRequestStatus.NotFound, ErrorCode: "RevisionNotFound");
            var original = await db.DefaultMembershipRevisions.AsNoTracking().Include(r => r.Rules).SingleOrDefaultAsync(r =>
                r.CompanyId == user.CompanyId && r.PolicyId == policy.PolicyId && r.RevisionId == restoreId.Value, ct);
            if (original is null) return new(MembershipRequestStatus.NotFound, ErrorCode: "RevisionNotFound");
            input = original.Rules.Select(RuleDto).ToArray();
        }
        var invalid = DefaultMembershipRuleValidation.Normalize(input, out var rules);
        if (invalid is not null) return new(MembershipRequestStatus.Invalid, ErrorCode: invalid);
        var roleIds = rules.Where(r => r.RoleId.HasValue).Select(r => r.RoleId!.Value).Distinct().ToArray();
        var categoryIds = rules.Where(r => r.ProductCategoryId.HasValue).Select(r => r.ProductCategoryId!.Value).Distinct().ToArray();
        if (await db.Roles.CountAsync(r => roleIds.Contains(r.Id), ct) != roleIds.Length ||
            await db.Categories.CountAsync(c => c.CompanyId == user.CompanyId && c.IsActive != false && categoryIds.Contains(c.CategoryId), ct) != categoryIds.Length)
            return new(MembershipRequestStatus.Invalid, ErrorCode: "RoleOrCategoryUnavailableInCompany");
        if (!await db.Employees.AnyAsync(e => e.EmployeeId == user.EmployeeId && e.CompanyId == user.CompanyId && e.IsActive, ct))
            return new(MembershipRequestStatus.Forbidden);
        try
        {
            if (policy is null)
            {
                policy = new DefaultMembershipPolicy { PolicyId = Guid.CreateVersion7(), CompanyId = user.CompanyId.Value, GroupType = type };
                db.DefaultMembershipPolicies.Add(policy);
                await db.SaveChangesAsync(ct); // nullable pointer breaks the FK cycle, inside this same transaction.
            }
            var revision = new DefaultMembershipRevision
            {
                RevisionId = Guid.CreateVersion7(), CompanyId = policy.CompanyId, PolicyId = policy.PolicyId, Policy = policy,
                Version = policy.Version + 1, CreatedBy = user.EmployeeId!.Value, CreatedAt = clock.Now,
                Reason = reason.Trim(), RestoredFromRevisionId = restoreId
            };
            foreach (var rule in rules) revision.Rules.Add(new DefaultMembershipRule
            {
                RuleId = Guid.CreateVersion7(), CompanyId = policy.CompanyId, PolicyId = policy.PolicyId, RevisionId = revision.RevisionId,
                SourceType = rule.SourceType, RoleId = rule.RoleId, GroupTypeCode = rule.GroupTypeCode,
                CategoryScope = rule.CategoryScope, ProductCategoryId = rule.ProductCategoryId
            });
            db.DefaultMembershipRevisions.Add(revision);
            await db.SaveChangesAsync(ct);
            policy.ActiveRevision = revision;
            policy.ActiveRevisionId = revision.RevisionId;
            policy.Version = revision.Version;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(MembershipRequestStatus.Success, Dto(policy));
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(MembershipRequestStatus.Conflict, ErrorCode: "ExpectedVersionMismatch");
        }
        catch (DbUpdateException)
        {
            return new(MembershipRequestStatus.Conflict, ErrorCode: "ConfigurationOrReferencesChangedRetry");
        }
    }
}
