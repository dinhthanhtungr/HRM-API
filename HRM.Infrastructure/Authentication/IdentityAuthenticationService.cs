using HRM.Application.Abstractions.Authentication;
using HRM.Application.Features.Auth.Contracts;
using HRM.Domain.Identity;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Infrastructure.Authentication;

public sealed class IdentityAuthenticationService(
    UserManager<ApplicationUser> userManager ,
    ApplicationDbContext dbContext,
    ILogger<IdentityAuthenticationService> logger)
    : IIdentityAuthenticationService
{
    public async Task<AuthenticatedUserDto?> ValidateUserAsync(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByNameAsync(userNameOrEmail)
            ?? await userManager.FindByEmailAsync(userNameOrEmail);

        if (user is null)
        {
            return null;
        }

        // Temporary compatibility: AspNetUsers does not have an IsActive column yet.
        if (await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        var isPasswordValid = await userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
        {
            await userManager.AccessFailedAsync(user);
            return null;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var employeeAccess = await GetEmployeeAccessAsync(user, cancellationToken);
        if (user.EmployeeId.HasValue && employeeAccess is null)
        {
            return null;
        }

        var roles = await GetActiveRolesAsync(user, cancellationToken);

        return new AuthenticatedUserDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            EmployeeId = user.EmployeeId,
            CompanyId = employeeAccess?.CompanyId,
            Roles = roles.ToArray()
        };
    }

    public async Task StoreRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("User not found or inactive.");
        }

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpirationDateTime = expiresAtUtc;

        await userManager.UpdateAsync(user);
    }

    public async Task<AuthenticatedUserDto?> ValidateRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return null;
            }

            var user = await userManager.Users
                .FirstOrDefaultAsync(
                    x => x.RefreshToken == refreshToken &&
                         x.RefreshTokenExpirationDateTime > DateTime.Now,
                    cancellationToken);

            if (user is null)
            {
                return null;
            }

            var employeeAccess = await GetEmployeeAccessAsync(user, cancellationToken);
            if (user.EmployeeId.HasValue && employeeAccess is null)
            {
                return null;
            }

            var roles = await GetActiveRolesAsync(user, cancellationToken);

            return new AuthenticatedUserDto
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                EmployeeId = user.EmployeeId,
                CompanyId = employeeAccess?.CompanyId,
                Roles = roles
            };
        }

    public async Task RevokeRefreshTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.RefreshToken = null;
        user.RefreshTokenExpirationDateTime = DateTime.MinValue;

        await userManager.UpdateAsync(user);
    }

    private async Task<string[]> GetActiveRolesAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var roleAssignments = await (
                from userRole in dbContext.UserRoles
                join role in dbContext.Roles on userRole.RoleId equals role.Id
                where userRole.UserId == user.Id &&
                      userRole.IsActive
                select role.Name)
            .ToListAsync(cancellationToken);

        var activeRoles = roleAssignments
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Select(roleName => roleName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logger.LogInformation(
            "Active identity roles for user {UserId}/{UserName}: ActiveCount={ActiveRoleCount}, TotalAssignments={TotalRoleAssignments}, Roles={Roles}",
            user.Id,
            user.UserName,
            activeRoles.Length,
            roleAssignments.Count,
            string.Join(", ", activeRoles));

        return activeRoles;
    }

    private async Task<EmployeeAccess?> GetEmployeeAccessAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (!user.EmployeeId.HasValue)
        {
            return null;
        }

        return await dbContext.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.EmployeeId == user.EmployeeId.Value &&
                employee.IsActive)
            .Select(employee => new EmployeeAccess(employee.CompanyId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed record EmployeeAccess(Guid? CompanyId);
}
