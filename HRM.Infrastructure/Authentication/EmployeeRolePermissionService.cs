using HRM.Application.Abstractions.Identity;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Employees.Administration;
using HRM.Application.Features.Employees.Dtos;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.Authentication;

public sealed class EmployeeRolePermissionService(ApplicationDbContext dbContext)
    : IEmployeeRolePermissionService
{
    public async Task<EmployeeRolePermissionsDto?> GetAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == roleId, cancellationToken);
        if (role?.Name is null) return null;

        var claims = await dbContext.RoleClaims.AsNoTracking()
            .Where(item => item.RoleId == roleId).ToListAsync(cancellationToken);
        var usesDatabase = claims.Any(item =>
            item.ClaimType == ApplicationPermissionClaimTypes.PermissionModelVersion &&
            item.ClaimValue == ApplicationPermissionClaimTypes.CurrentModelVersion);
        var permissions = ApplicationPermissionCatalog.ResolveRole(role.Name, usesDatabase,
            claims.Where(item => item.ClaimType == ApplicationPermissionClaimTypes.Permission)
                .Select(item => item.ClaimValue ?? string.Empty));
        return new(role.Id, role.Name, role.ConcurrencyStamp ?? "unversioned",
            usesDatabase, permissions, ApplicationPermissionCatalog.Codes);
    }

    public async Task<EmployeeAdministrationResult<EmployeeRolePermissionsDto>> ReplaceAsync(
        Guid roleId, string version, IReadOnlyList<string> permissions, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.FirstOrDefaultAsync(item => item.Id == roleId, cancellationToken);
        if (role?.Name is null)
            return EmployeeAdministrationResult<EmployeeRolePermissionsDto>.Fail(
                EmployeeAdministrationError.NotFound, "Không tìm thấy role.");
        if ((role.ConcurrencyStamp ?? "unversioned") != version)
            return Conflict();

        var existing = await dbContext.RoleClaims.Where(item => item.RoleId == roleId &&
            (item.ClaimType == ApplicationPermissionClaimTypes.Permission ||
             item.ClaimType == ApplicationPermissionClaimTypes.PermissionModelVersion)).ToListAsync(cancellationToken);
        // Chỉ thay claim do màn này sở hữu; giữ claim khác và permission ngoài catalog của phiên bản hiện tại.
        dbContext.RoleClaims.RemoveRange(existing.Where(item =>
            item.ClaimType == ApplicationPermissionClaimTypes.PermissionModelVersion ||
            ApplicationPermissionCatalog.Codes.Contains(item.ClaimValue ?? string.Empty, StringComparer.Ordinal)));
        foreach (var code in permissions)
            dbContext.RoleClaims.Add(new IdentityRoleClaim<Guid>
            {
                RoleId = roleId, ClaimType = ApplicationPermissionClaimTypes.Permission, ClaimValue = code
            });
        dbContext.RoleClaims.Add(new IdentityRoleClaim<Guid>
        {
            RoleId = roleId,
            ClaimType = ApplicationPermissionClaimTypes.PermissionModelVersion,
            ClaimValue = ApplicationPermissionClaimTypes.CurrentModelVersion
        });
        role.ConcurrencyStamp = Guid.NewGuid().ToString();
        try
        {
            // Một SaveChanges: role version và toàn bộ claims commit/rollback cùng nhau.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        return EmployeeAdministrationResult<EmployeeRolePermissionsDto>.Ok(
            new(role.Id, role.Name, role.ConcurrencyStamp, true,
                permissions.Order(StringComparer.Ordinal).ToArray(), ApplicationPermissionCatalog.Codes));
    }

    private static EmployeeAdministrationResult<EmployeeRolePermissionsDto> Conflict()
        => EmployeeAdministrationResult<EmployeeRolePermissionsDto>.Fail(
            EmployeeAdministrationError.Conflict, "Role đã được thay đổi. Tải lại trước khi lưu.");
}
