using HRM.Application.Abstractions.Identity;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Identity;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.Authentication;

public sealed class IdentityAccessValidator(ApplicationDbContext dbContext)
    : IIdentityAccessValidator
{
    public async Task<bool> IsAccessAllowedAsync(
        Guid userId,
        Guid? employeeId,
        Guid? companyId,
        bool tokenHasImsRole,
        CancellationToken cancellationToken = default)
    {
        var imsRole = ApplicationRoles.InternalSystems.IMSUser;
        var normalizedImsRole = imsRole.ToUpperInvariant();
        var activeImsAssignments =
            from assignment in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.IsActive && (role.NormalizedName == normalizedImsRole ||
                (role.Name != null && role.Name.ToUpper() == normalizedImsRole))
            select assignment.UserId;

        var account = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.EmployeeId, user.LockoutEnabled, user.LockoutEnd,
                HasImsRole = activeImsAssignments.Contains(user.Id) })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null || account.EmployeeId != employeeId || account.HasImsRole != tokenHasImsRole ||
            IdentityAccountAccessRules.IsLocked(account.LockoutEnabled, account.LockoutEnd, DateTimeOffset.UtcNow))
        {
            return false;
        }

        if (!account.EmployeeId.HasValue)
        {
            return !companyId.HasValue;
        }

        return await dbContext.Employees
            .AsNoTracking()
            .AnyAsync(employee =>
                employee.EmployeeId == account.EmployeeId.Value &&
                employee.IsActive &&
                employee.CompanyId == companyId,
                cancellationToken);
    }
}
