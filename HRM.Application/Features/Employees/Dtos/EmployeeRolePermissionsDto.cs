namespace HRM.Application.Features.Employees.Dtos;

public sealed record EmployeeRolePermissionsDto(
    Guid RoleId,
    string RoleName,
    string Version,
    bool UsesDatabasePermissions,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> AvailablePermissions);
