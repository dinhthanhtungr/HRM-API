using HRM.Application.Features.Employees.Administration;
using HRM.Application.Features.Employees.Dtos;

namespace HRM.Application.Abstractions.Identity;

public interface IEmployeeRolePermissionService
{
    Task<EmployeeRolePermissionsDto?> GetAsync(Guid roleId, CancellationToken cancellationToken);
    Task<EmployeeAdministrationResult<EmployeeRolePermissionsDto>> ReplaceAsync(
        Guid roleId, string version, IReadOnlyList<string> permissions, CancellationToken cancellationToken);
}
