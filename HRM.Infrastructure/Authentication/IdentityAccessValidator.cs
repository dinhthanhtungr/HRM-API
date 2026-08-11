using HRM.Application.Abstractions.Identity;
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
            .Select(user => new { user.IsActive, user.EmployeeId })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null ||
            !account.IsActive ||
            account.EmployeeId != employeeId)
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
