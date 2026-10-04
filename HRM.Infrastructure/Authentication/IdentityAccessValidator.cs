using HRM.Application.Abstractions.Identity;
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
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.EmployeeId, user.LockoutEnabled, user.LockoutEnd })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null || account.EmployeeId != employeeId ||
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
