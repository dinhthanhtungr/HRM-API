using HRM.Application.Abstractions.Identity;
using HRM.Domain.Identity;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.Authentication;

public sealed class EmployeeIdentityAdministrationService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager)
    : IEmployeeIdentityAdministrationService
{
    public async Task<EmployeeIdentityAccount?> GetAccountAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users
            .AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .Select(item => new
            {
                item.Id,
                item.UserName,
                item.Email
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = await (
                from assignment in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking()
                    on assignment.RoleId equals role.Id
                where assignment.UserId == user.Id &&
                      assignment.IsActive &&
                      role.Name != null
                orderby role.Name
                select role.Name!)
            .ToListAsync(cancellationToken);

        return new EmployeeIdentityAccount(
            user.Id,
            user.UserName,
            user.Email,
            true,
            roles);
    }

    public async Task<IReadOnlyList<EmployeeIdentityRole>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        return await roleManager.Roles
            .AsNoTracking()
            .Where(role => role.Name != null)
            .OrderBy(role => role.Name)
            .Select(role => new EmployeeIdentityRole(
                role.Id,
                role.Name!,
                dbContext.UserRoles.Count(assignment =>
                    assignment.RoleId == role.Id &&
                    assignment.IsActive)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IdentityAdministrationResult<EmployeeIdentityAccount>> CreateAccountAsync(
        Guid employeeId,
        string userName,
        string? email,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await userManager.Users.AnyAsync(
                user => user.EmployeeId == employeeId,
                cancellationToken))
        {
            return IdentityAdministrationResult<EmployeeIdentityAccount>.Fail(
                "Nhân viên đã có tài khoản.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            EmployeeId = employeeId,
            UserName = userName,
            Email = email,
            personName = null,
            RefreshTokenExpirationDateTime = DateTime.MinValue,
            UserRoles = []
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return IdentityAdministrationResult<EmployeeIdentityAccount>.Fail(
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        return IdentityAdministrationResult<EmployeeIdentityAccount>.Ok(
            new EmployeeIdentityAccount(
                user.Id,
                user.UserName,
                user.Email,
                true,
                []));
    }

    public async Task<IdentityAdministrationResult<EmployeeIdentityAccount>> SetAccountActiveAsync(
        Guid employeeId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(item => item.EmployeeId == employeeId, cancellationToken);
        if (user is null)
        {
            return IdentityAdministrationResult<EmployeeIdentityAccount>.Fail(
                "Nhân viên chưa có tài khoản.");
        }

        // Temporary compatibility: AspNetUsers does not have an IsActive column yet.
        // Restore account status persistence after the database schema is updated.
        if (!isActive)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpirationDateTime = DateTime.MinValue;
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return IdentityAdministrationResult<EmployeeIdentityAccount>.Fail(
                string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        var account = await GetAccountAsync(employeeId, cancellationToken);
        return account is null
            ? IdentityAdministrationResult<EmployeeIdentityAccount>.Fail(
                "Không thể tải lại tài khoản nhân viên.")
            : IdentityAdministrationResult<EmployeeIdentityAccount>.Ok(account);
    }

    public async Task<IdentityAdministrationResult> AssignRoleAsync(
        Guid employeeId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        var assignmentContext = await ResolveAssignmentAsync(
            employeeId,
            roleName,
            cancellationToken);
        if (assignmentContext.Error is not null)
        {
            return IdentityAdministrationResult.Fail(assignmentContext.Error);
        }

        var assignment = await dbContext.UserRoles.FirstOrDefaultAsync(
            item =>
                item.UserId == assignmentContext.UserId &&
                item.RoleId == assignmentContext.RoleId,
            cancellationToken);

        if (assignment is null)
        {
            dbContext.UserRoles.Add(new ApplicationUserRole
            {
                UserId = assignmentContext.UserId,
                RoleId = assignmentContext.RoleId,
                IsActive = true
            });
        }
        else
        {
            assignment.IsActive = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return IdentityAdministrationResult.Ok();
    }

    public async Task<IdentityAdministrationResult> RevokeRoleAsync(
        Guid employeeId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        var assignmentContext = await ResolveAssignmentAsync(
            employeeId,
            roleName,
            cancellationToken);
        if (assignmentContext.Error is not null)
        {
            return IdentityAdministrationResult.Fail(assignmentContext.Error);
        }

        var assignment = await dbContext.UserRoles.FirstOrDefaultAsync(
            item =>
                item.UserId == assignmentContext.UserId &&
                item.RoleId == assignmentContext.RoleId &&
                item.IsActive,
            cancellationToken);

        if (assignment is null)
        {
            return IdentityAdministrationResult.Fail(
                "Nhân viên chưa được cấp quyền này.");
        }

        assignment.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return IdentityAdministrationResult.Ok();
    }

    public async Task<IdentityAdministrationResult<EmployeeIdentityRole>> CreateRoleAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await roleManager.RoleExistsAsync(roleName))
        {
            return IdentityAdministrationResult<EmployeeIdentityRole>.Fail(
                "Loại quyền đã tồn tại.");
        }

        var role = new ApplicationRole
        {
            Id = Guid.CreateVersion7(),
            Name = roleName,
            UserRoles = []
        };
        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            return IdentityAdministrationResult<EmployeeIdentityRole>.Fail(
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        return IdentityAdministrationResult<EmployeeIdentityRole>.Ok(
            new EmployeeIdentityRole(role.Id, role.Name!, 0));
    }

    private async Task<AssignmentContext> ResolveAssignmentAsync(
        Guid employeeId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var userId = await userManager.Users
            .Where(user => user.EmployeeId == employeeId)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (userId is null)
        {
            return AssignmentContext.Fail("Nhân viên chưa có tài khoản.");
        }

        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return AssignmentContext.Fail("Loại quyền không tồn tại.");
        }

        return AssignmentContext.Ok(userId.Value, role.Id);
    }

    private sealed record AssignmentContext(Guid UserId, Guid RoleId, string? Error)
    {
        public static AssignmentContext Ok(Guid userId, Guid roleId)
            => new(userId, roleId, null);

        public static AssignmentContext Fail(string error)
            => new(Guid.Empty, Guid.Empty, error);
    }
}
