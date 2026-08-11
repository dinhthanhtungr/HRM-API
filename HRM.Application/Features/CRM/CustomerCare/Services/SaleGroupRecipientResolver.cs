using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using Microsoft.EntityFrameworkCore;
using SystemGroupType = Shared.Enums.GroupType;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

internal sealed class SaleGroupRecipientResolver : ISaleGroupRecipientResolver
{
    private readonly ICRMReadDbContext _crmDbContext;
    private readonly IInternalMailDbContext _identityDbContext;

    public SaleGroupRecipientResolver(
        ICRMReadDbContext crmDbContext,
        IInternalMailDbContext identityDbContext)
    {
        _crmDbContext = crmDbContext;
        _identityDbContext = identityDbContext;
    }

    public async Task<IReadOnlyCollection<Guid>> ResolveSaleGroupLeaderIdsAsync(
        Guid companyId,
        Guid saleEmployeeId,
        Guid? excludeEmployeeId = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty || saleEmployeeId == Guid.Empty)
        {
            return Array.Empty<Guid>();
        }

        var saleGroupType = SystemGroupType.CMR;
        var saleGroupTypePrefix = SystemGroupType.CMR + ".%";

        var saleGroupIds = await _crmDbContext.MemberInGroups
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.Profile == saleEmployeeId &&
                x.Group.CompanyId == companyId &&
                (x.Group.GroupType == saleGroupType ||
                 x.Group.GroupType != null &&
                 EF.Functions.Like(x.Group.GroupType, saleGroupTypePrefix)))
            .Select(x => x.GroupId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (saleGroupIds.Length == 0)
        {
            return Array.Empty<Guid>();
        }

        var query = _crmDbContext.MemberInGroups
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.IsAdmin == true &&
                x.Profile.HasValue &&
                saleGroupIds.Contains(x.GroupId) &&
                x.ProfileNavigation != null &&
                x.ProfileNavigation.CompanyId == companyId &&
                x.ProfileNavigation.IsActive);

        if (excludeEmployeeId is { } excluded && excluded != Guid.Empty)
        {
            query = query.Where(x => x.Profile != excluded);
        }

        return await query
            .Select(x => x.Profile!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> ResolveActiveEmployeeIdsByRolesAsync(
        Guid companyId,
        IReadOnlyCollection<string> normalizedRoleNames,
        Guid? excludeEmployeeId = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty || normalizedRoleNames.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var query =
            from role in _identityDbContext.Roles.AsNoTracking()
            join userRole in _identityDbContext.UserRoles.AsNoTracking()
                on role.Id equals userRole.RoleId
            join user in _identityDbContext.Users.AsNoTracking()
                on userRole.UserId equals user.Id
            join employee in _identityDbContext.Employees.AsNoTracking()
                on user.EmployeeId equals employee.EmployeeId
            where role.NormalizedName != null &&
                  normalizedRoleNames.Contains(role.NormalizedName) &&
                  userRole.IsActive &&
                  user.EmployeeId.HasValue &&
                  employee.CompanyId == companyId &&
                  employee.IsActive
            select employee.EmployeeId;

        if (excludeEmployeeId is { } excluded && excluded != Guid.Empty)
        {
            query = query.Where(employeeId => employeeId != excluded);
        }

        return await query
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

}
