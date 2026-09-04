using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

public sealed class SampleRequestRecipientResolver
{
    private static readonly IReadOnlyList<string> RequiredNormalizedRoleNames = SampleRequestRecipientRules
        .RequiredMessageRecipientRoles
        .Select(x => x.ToUpperInvariant())
        .ToArray();

    private static readonly IReadOnlyList<string> RequiredNormalizedUserNames = SampleRequestRecipientRules
        .RequiredMessageRecipientUserNames
        .Select(x => x.ToUpperInvariant())
        .ToArray();

    private static readonly IReadOnlyList<string> SilentWatcherNormalizedRoleNames = SampleRequestRecipientRules
        .SilentWatcherRoleNames
        .Select(x => x.ToUpperInvariant())
        .ToArray();

    private static readonly string NormalizedSaleUserRoleName = ApplicationRoles.Sales.SaleUser.ToUpperInvariant();

    private readonly IInternalMailDbContext _dbContext;

    public SampleRequestRecipientResolver(IInternalMailDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SampleRequestRecipientDto>> ResolveDefaultMessageRecipientsAsync(
        Guid companyId,
        string? productCategoryExternalId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Array.Empty<SampleRequestRecipientDto>();
        }

        var roleRecipients = await (
                from role in _dbContext.Roles.AsNoTracking()
                join userRole in _dbContext.UserRoles.AsNoTracking()
                    on role.Id equals userRole.RoleId
                join user in _dbContext.Users.AsNoTracking()
                    on userRole.UserId equals user.Id
                join employee in _dbContext.Employees.AsNoTracking()
                    on user.EmployeeId equals employee.EmployeeId
                where userRole.IsActive &&
                      user.EmployeeId.HasValue &&
                      employee.CompanyId == companyId &&
                      employee.IsActive &&
                      ((role.Name != null && SampleRequestRecipientRules.RequiredMessageRecipientRoles.Contains(role.Name)) ||
                       (role.NormalizedName != null && RequiredNormalizedRoleNames.Contains(role.NormalizedName)))
                orderby employee.FullName
                select new SampleRequestRecipientDto
                {
                    EmployeeId = employee.EmployeeId,
                    FullName = employee.FullName,
                    ExternalId = employee.ExternalId,
                    Source = SampleRequestRecipientSources.Required,
                    Reason = "Required sample request recipient",
                    Locked = true
                })
            .Distinct()
            .ToListAsync(cancellationToken);

        var groupLeaderRecipients = await ResolveDefaultGroupLeaderRecipientsAsync(
            companyId,
            productCategoryExternalId,
            cancellationToken);

        var requiredAccountRecipients = await (
                from user in _dbContext.Users.AsNoTracking()
                join employee in _dbContext.Employees.AsNoTracking()
                    on user.EmployeeId equals employee.EmployeeId
                where user.EmployeeId.HasValue &&
                      employee.CompanyId == companyId &&
                      employee.IsActive &&
                      user.NormalizedUserName != null &&
                      RequiredNormalizedUserNames.Contains(user.NormalizedUserName)
                orderby employee.FullName
                select new SampleRequestRecipientDto
                {
                    EmployeeId = employee.EmployeeId,
                    FullName = employee.FullName,
                    ExternalId = employee.ExternalId,
                    Source = SampleRequestRecipientSources.Required,
                    Reason = "Required sample request account recipient",
                    Locked = true
                })
            .Distinct()
            .ToListAsync(cancellationToken);

        return roleRecipients
            .Concat(requiredAccountRecipients)
            .Concat(groupLeaderRecipients)
            .GroupBy(x => x.EmployeeId)
            .Select(x => x.First())
            .OrderBy(x => x.FullName)
            .ToList();
    }

    public async Task<IReadOnlyList<SampleRequestRecipientDto>> ResolveDefaultSilentWatchersAsync(
        Guid companyId,
        Guid currentEmployeeId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Array.Empty<SampleRequestRecipientDto>();
        }

        return await (
                from role in _dbContext.Roles.AsNoTracking()
                join userRole in _dbContext.UserRoles.AsNoTracking()
                    on role.Id equals userRole.RoleId
                join user in _dbContext.Users.AsNoTracking()
                    on userRole.UserId equals user.Id
                join employee in _dbContext.Employees.AsNoTracking()
                    on user.EmployeeId equals employee.EmployeeId
                where userRole.IsActive &&
                      user.EmployeeId.HasValue &&
                      employee.EmployeeId != currentEmployeeId &&
                      employee.CompanyId == companyId &&
                      employee.IsActive &&
                      ((role.Name != null && SampleRequestRecipientRules.SilentWatcherRoleNames.Contains(role.Name)) ||
                       (role.NormalizedName != null && SilentWatcherNormalizedRoleNames.Contains(role.NormalizedName)))
                orderby employee.FullName
                select new SampleRequestRecipientDto
                {
                    EmployeeId = employee.EmployeeId,
                    FullName = employee.FullName,
                    ExternalId = employee.ExternalId,
                    Source = SampleRequestRecipientSources.SilentWatcher,
                    Reason = "Lab role watcher without notifications by default",
                    Locked = false
                })
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Sales gửi yêu cầu phối mẫu sẽ luôn CC leader active của cùng group.
    /// Group là ranh giới nghiệp vụ; không dùng role Leader toàn công ty.
    /// </summary>
    public async Task<IReadOnlyList<SampleRequestRecipientDto>> ResolveSalesGroupLeaderRecipientsAsync(
        Guid companyId,
        Guid senderEmployeeId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty || senderEmployeeId == Guid.Empty)
        {
            return Array.Empty<SampleRequestRecipientDto>();
        }

        var senderIsSale = await (
                from user in _dbContext.Users.AsNoTracking()
                join userRole in _dbContext.UserRoles.AsNoTracking()
                    on user.Id equals userRole.UserId
                join role in _dbContext.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where user.EmployeeId == senderEmployeeId &&
                      userRole.IsActive &&
                      ((role.Name != null && role.Name == ApplicationRoles.Sales.SaleUser) ||
                       (role.NormalizedName != null && role.NormalizedName == NormalizedSaleUserRoleName))
                select user.Id)
            .AnyAsync(cancellationToken);
        if (!senderIsSale)
        {
            return Array.Empty<SampleRequestRecipientDto>();
        }

        return await (
                from senderMembership in _dbContext.MemberInGroups.AsNoTracking()
                join groupItem in _dbContext.Groups.AsNoTracking()
                    on senderMembership.GroupId equals groupItem.GroupId
                join leaderMembership in _dbContext.MemberInGroups.AsNoTracking()
                    on groupItem.GroupId equals leaderMembership.GroupId
                join leader in _dbContext.Employees.AsNoTracking()
                    on leaderMembership.Profile equals leader.EmployeeId
                where senderMembership.Profile == senderEmployeeId &&
                      senderMembership.IsActive &&
                      groupItem.CompanyId == companyId &&
                      leaderMembership.IsActive &&
                      leaderMembership.IsAdmin == true &&
                      leaderMembership.Profile.HasValue &&
                      leader.EmployeeId != senderEmployeeId &&
                      leader.CompanyId == companyId &&
                      leader.IsActive
                orderby leader.FullName
                select new SampleRequestRecipientDto
                {
                    EmployeeId = leader.EmployeeId,
                    FullName = leader.FullName,
                    ExternalId = leader.ExternalId,
                    Source = SampleRequestRecipientSources.SalesGroupLeader,
                    Reason = "Sales group leader of the sender",
                    Locked = true
                })
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SampleRequestRecipientDto>> ResolveDefaultGroupLeaderRecipientsAsync(
        Guid companyId,
        string? productCategoryExternalId,
        CancellationToken cancellationToken)
    {
        var defaultLeaderGroupTypes = SampleRequestRecipientRules.ResolveDefaultLeaderGroupTypes(productCategoryExternalId);

        return await (
                from groupItem in _dbContext.Groups.AsNoTracking()
                join member in _dbContext.MemberInGroups.AsNoTracking()
                    on groupItem.GroupId equals member.GroupId
                join employee in _dbContext.Employees.AsNoTracking()
                    on member.Profile equals employee.EmployeeId
                where groupItem.CompanyId == companyId &&
                      groupItem.GroupType != null &&
                      defaultLeaderGroupTypes.Contains(groupItem.GroupType) &&
                      member.IsActive &&
                      member.IsAdmin == true &&
                      member.Profile.HasValue &&
                      employee.CompanyId == companyId &&
                      employee.IsActive
                orderby employee.FullName
                select new SampleRequestRecipientDto
                {
                    EmployeeId = employee.EmployeeId,
                    FullName = employee.FullName,
                    ExternalId = employee.ExternalId,
                    Source = SampleRequestRecipientSources.Default,
                    Reason = "Default sample request group leader",
                    Locked = false
                })
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}

public sealed class SampleRequestRecipientDto
{
    public Guid EmployeeId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? ExternalId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool Locked { get; init; }
}

public static class SampleRequestRecipientSources
{
    public const string Required = "required";
    public const string Default = "default";
    public const string SilentWatcher = "silent_watcher";
    public const string SalesGroupLeader = "sales_group_leader";
}
