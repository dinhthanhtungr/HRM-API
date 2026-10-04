using HRM.Application.Abstractions.Identity;
using HRM.Application.Features.Employees.Dtos;

namespace HRM.Application.Features.Employees.Administration;

internal static class EmployeeAccountPermissionsMapper
{
    public static EmployeeAccountPermissionsDto Map(
        Guid employeeId, bool employeeIsActive, EmployeeIdentityAccount? account, DateOnly? endDate = null)
        => new()
        {
            EmployeeId = employeeId,
            EmployeeIsActive = employeeIsActive,
            EndDate = endDate,
            HasAccount = account is not null,
            UserId = account?.UserId,
            UserName = account?.UserName,
            Email = account?.Email,
            AccountIsActive = account?.IsActive,
            Roles = account?.ActiveRoles ?? [],
            Permissions = account?.Permissions ?? []
        };
}
