using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Auth.Contracts;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.Authentication;

public sealed partial class IdentityAuthenticationService
{
    public async Task<AuthenticatedUserDto?> ResolveDeveloperSampleWorkUserAsync(
        Guid actorUserId, Guid companyId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var actor = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
        if (actor is null || await userManager.IsLockedOutAsync(actor) ||
            (await GetEmployeeAccessAsync(actor, cancellationToken))?.CompanyId != companyId)
            return null;
        var actorAuthorization = await GetAuthorizationAsync(actor, cancellationToken);
        if (!actorAuthorization.Roles.Contains(ApplicationRoles.Developer, StringComparer.OrdinalIgnoreCase))
            return null;
        var users = await dbContext.Users.AsNoTracking().Where(x => x.EmployeeId == employeeId)
            .Take(2).ToListAsync(cancellationToken);
        if (users.Count != 1) return null;
        var user = users[0];
        if (await userManager.IsLockedOutAsync(user) ||
            (await GetEmployeeAccessAsync(user, cancellationToken))?.CompanyId != companyId)
            return null;
        var authorization = await GetAuthorizationAsync(user, cancellationToken);
        if (!authorization.Roles.Any(x => x is ApplicationRoles.Lab.LabUser or
                ApplicationRoles.Lab.LabAdmin or ApplicationRoles.Sales.SaleUser or ApplicationRoles.Sales.SaleAdmin))
            return null;
        return new AuthenticatedUserDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            EmployeeId = employeeId,
            CompanyId = companyId,
            Roles = authorization.Roles,
            Permissions = authorization.Permissions,
            UsesDatabasePermissions = authorization.UsesDatabasePermissions
        };
    }
}
