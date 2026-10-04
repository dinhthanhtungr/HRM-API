using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.GetEmployeeRolePermissions;

internal sealed class GetEmployeeRolePermissionsQueryHandler(
    ICurrentUser currentUser, IEmployeeRolePermissionService service)
    : IRequestHandler<GetEmployeeRolePermissionsQuery, EmployeeAdministrationResult<EmployeeRolePermissionsDto>>
{
    public async Task<EmployeeAdministrationResult<EmployeeRolePermissionsDto>> Handle(
        GetEmployeeRolePermissionsQuery request, CancellationToken cancellationToken)
    {
        if (!RolePermissionAdministrationRules.CanManage(currentUser))
            return EmployeeAdministrationResult<EmployeeRolePermissionsDto>.Fail(
                EmployeeAdministrationError.Forbidden, "Chỉ Developer được cấu hình quyền của role toàn hệ thống.");

        var result = await service.GetAsync(request.RoleId, cancellationToken);
        return result is null
            ? EmployeeAdministrationResult<EmployeeRolePermissionsDto>.Fail(
                EmployeeAdministrationError.NotFound, "Không tìm thấy role.")
            : EmployeeAdministrationResult<EmployeeRolePermissionsDto>.Ok(result);
    }
}
