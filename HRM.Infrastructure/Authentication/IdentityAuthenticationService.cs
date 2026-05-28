using HRM.Application.Abstractions.Authentication;
using HRM.Application.Features.Auth.Contracts;
using HRM.Domain.Identity;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.Authentication;

public sealed class IdentityAuthenticationService(
    UserManager<ApplicationUser> userManager ,
    ApplicationDbContext dbContext)
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

        var roles = await userManager.GetRolesAsync(user);

        Guid? companyId = null;

        if (user.EmployeeId.HasValue)
        {
            companyId = await dbContext.Employees
                .Where(x => x.EmployeeId == user.EmployeeId.Value)
                .Select(x => x.CompanyId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new AuthenticatedUserDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            EmployeeId = user.EmployeeId,
            CompanyId = companyId,
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
            throw new InvalidOperationException("User not found.");
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

            var roles = await userManager.GetRolesAsync(user);

            Guid? companyId = null;

            if (user.EmployeeId.HasValue)
            {
                companyId = await dbContext.Employees
                    .Where(x => x.EmployeeId == user.EmployeeId.Value)
                    .Select(x => x.CompanyId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return new AuthenticatedUserDto
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                EmployeeId = user.EmployeeId,
                CompanyId = companyId,
                Roles = roles.ToArray()
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
}   