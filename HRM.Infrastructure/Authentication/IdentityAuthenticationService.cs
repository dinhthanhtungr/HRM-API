using HRM.Application.Abstractions.Authentication;
using HRM.Application.Features.Auth.Contracts;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Identity;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRM.Infrastructure.Authentication;

public sealed class IdentityAuthenticationService(
    UserManager<ApplicationUser> userManager ,
    ApplicationDbContext dbContext,
    ILogger<IdentityAuthenticationService> logger,
    IOptions<RefreshTokenSessionOptions> refreshTokenOptions)
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

        var authorization = await GetAuthorizationAsync(user, cancellationToken);

        return new AuthenticatedUserDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            EmployeeId = user.EmployeeId,
            CompanyId = employeeAccess?.CompanyId,
            Roles = authorization.Roles,
            Permissions = authorization.Permissions,
            UsesDatabasePermissions = authorization.UsesDatabasePermissions
        };
    }

    public async Task<RefreshTokenSessionDto> StoreOrReuseRefreshTokenAsync(
        Guid userId,
        string proposedRefreshToken,
        DateTime proposedExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("User not found or inactive.");
        }

        if (refreshTokenOptions.Value.ReuseActiveToken &&
            !string.IsNullOrWhiteSpace(user.RefreshToken) &&
            user.RefreshTokenExpirationDateTime > DateTime.Now)
        {
            return new RefreshTokenSessionDto(
                user.RefreshToken,
                user.RefreshTokenExpirationDateTime);
        }

        user.RefreshToken = proposedRefreshToken;
        user.RefreshTokenExpirationDateTime = proposedExpiresAtUtc;

        await userManager.UpdateAsync(user);

        return new RefreshTokenSessionDto(
            proposedRefreshToken,
            proposedExpiresAtUtc);
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

            var authorization = await GetAuthorizationAsync(user, cancellationToken);

            return new AuthenticatedUserDto
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                EmployeeId = user.EmployeeId,
                CompanyId = employeeAccess?.CompanyId,
                Roles = authorization.Roles,
                Permissions = authorization.Permissions,
                UsesDatabasePermissions = authorization.UsesDatabasePermissions
            };
        }

    public async Task<RefreshTokenSessionDto?> RenewRefreshTokenAsync(
        Guid userId,
        string expectedRefreshToken,
        string proposedRefreshToken,
        DateTime proposedExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(expectedRefreshToken) ||
            string.IsNullOrWhiteSpace(proposedRefreshToken))
        {
            return null;
        }

        var now = DateTime.Now;

        if (refreshTokenOptions.Value.ReuseActiveToken)
        {
            var activeSession = await userManager.Users
                .Where(user =>
                    user.Id == userId &&
                    user.RefreshToken == expectedRefreshToken &&
                    user.RefreshTokenExpirationDateTime > now)
                .Select(user => new RefreshTokenSessionDto(
                    user.RefreshToken!,
                    user.RefreshTokenExpirationDateTime))
                .FirstOrDefaultAsync(cancellationToken);

            return activeSession;
        }

        var updatedRows = await userManager.Users
            .Where(user =>
                user.Id == userId &&
                user.RefreshToken == expectedRefreshToken &&
                user.RefreshTokenExpirationDateTime > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(user => user.RefreshToken, proposedRefreshToken)
                    .SetProperty(user => user.RefreshTokenExpirationDateTime, proposedExpiresAtUtc),
                cancellationToken);

        return updatedRows == 1
            ? new RefreshTokenSessionDto(proposedRefreshToken, proposedExpiresAtUtc)
            : null;
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

    private async Task<UserAuthorizationSnapshot> GetAuthorizationAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var roleAssignments = await (
                from userRole in dbContext.UserRoles
                join role in dbContext.Roles on userRole.RoleId equals role.Id
                where userRole.UserId == user.Id &&
                      userRole.IsActive
                select new { role.Id, role.Name })
            .ToListAsync(cancellationToken);

        var activeRoles = roleAssignments
            .Where(role => !string.IsNullOrWhiteSpace(role.Name))
            .Select(role => role.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var activeRoleIds = roleAssignments
            .Select(role => role.Id)
            .Distinct()
            .ToArray();
        var roleClaims = await dbContext.RoleClaims
            .AsNoTracking()
            .Where(claim => activeRoleIds.Contains(claim.RoleId) &&
                (claim.ClaimType == ApplicationPermissionClaimTypes.Permission ||
                 claim.ClaimType == ApplicationPermissionClaimTypes.PermissionModelVersion))
            .Select(claim => new { claim.ClaimType, claim.ClaimValue })
            .ToListAsync(cancellationToken);
        var usesDatabasePermissions = roleClaims.Any(claim =>
            claim.ClaimType == ApplicationPermissionClaimTypes.PermissionModelVersion &&
            claim.ClaimValue == ApplicationPermissionClaimTypes.CurrentModelVersion);
        var permissions = roleClaims
            .Where(claim =>
                claim.ClaimType == ApplicationPermissionClaimTypes.Permission &&
                !string.IsNullOrWhiteSpace(claim.ClaimValue))
            .Select(claim => claim.ClaimValue!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logger.LogInformation(
            "Active identity roles for user {UserId}/{UserName}: ActiveCount={ActiveRoleCount}, TotalAssignments={TotalRoleAssignments}, Roles={Roles}",
            user.Id,
            user.UserName,
            activeRoles.Length,
            roleAssignments.Count,
            string.Join(", ", activeRoles));

        return new UserAuthorizationSnapshot(
            activeRoles,
            permissions,
            usesDatabasePermissions);
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

    private sealed record UserAuthorizationSnapshot(
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions,
        bool UsesDatabasePermissions);
}
