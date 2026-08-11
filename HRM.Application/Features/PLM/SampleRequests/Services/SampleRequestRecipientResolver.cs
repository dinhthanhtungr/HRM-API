using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

public sealed class SampleRequestRecipientResolver
{
    private static readonly IReadOnlyList<string> RequiredNormalizedRoleNames = SampleRequestRecipientRules
        .RequiredMessageRecipientRoles
        .Select(x => x.ToUpperInvariant())
        .ToArray();

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

        return roleRecipients
            .Concat(groupLeaderRecipients)
            .GroupBy(x => x.EmployeeId)
            .Select(x => x.First())
            .OrderBy(x => x.FullName)
            .ToList();
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
}
