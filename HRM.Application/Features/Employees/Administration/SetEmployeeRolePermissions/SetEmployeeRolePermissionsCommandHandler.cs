using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.SetEmployeeRolePermissions;

internal sealed class SetEmployeeRolePermissionsCommandHandler(
    ICurrentUser currentUser, IEmployeeRolePermissionService service)
    : IRequestHandler<SetEmployeeRolePermissionsCommand, EmployeeAdministrationResult<EmployeeRolePermissionsDto>>
{
    public Task<EmployeeAdministrationResult<EmployeeRolePermissionsDto>> Handle(
        SetEmployeeRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        if (!RolePermissionAdministrationRules.CanManage(currentUser))
            return Task.FromResult(EmployeeAdministrationResult<EmployeeRolePermissionsDto>.Fail(
                EmployeeAdministrationError.Forbidden, "Chỉ Developer được cấu hình quyền của role toàn hệ thống."));

        if (string.IsNullOrWhiteSpace(request.Version) || !RolePermissionAdministrationRules.IsValid(request.Permissions))
            return Task.FromResult(EmployeeAdministrationResult<EmployeeRolePermissionsDto>.Fail(
                EmployeeAdministrationError.Validation, "Version và danh sách permission hợp lệ là bắt buộc; không nhận quyền lạ hoặc trùng."));

        return service.ReplaceAsync(request.RoleId, request.Version, request.Permissions!, cancellationToken);
    }
}
