using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.CreateEmployeeRole;

internal sealed class CreateEmployeeRoleCommandHandler
    : IRequestHandler<
        CreateEmployeeRoleCommand,
        EmployeeAdministrationResult<EmployeeRoleLookupDto>>
{
    private readonly IEmployeeIdentityAdministrationService _identityService;
    private readonly ICurrentUser _currentUser;

    public CreateEmployeeRoleCommandHandler(
        IEmployeeIdentityAdministrationService identityService,
        ICurrentUser currentUser)
    {
        _identityService = identityService;
        _currentUser = currentUser;
    }

    public async Task<EmployeeAdministrationResult<EmployeeRoleLookupDto>> Handle(
        CreateEmployeeRoleCommand request,
        CancellationToken cancellationToken)
    {
        if (!EmployeeAdministrationRules.CanManageRoleTypes(_currentUser))
        {
            return EmployeeAdministrationResult<EmployeeRoleLookupDto>.Fail(
                EmployeeAdministrationError.Forbidden,
                "Chỉ Admin hoặc Developer được tạo loại quyền.");
        }

        var roleName = request.Name.Trim();
        if (!EmployeeAdministrationRules.IsValidRoleName(roleName))
        {
            return EmployeeAdministrationResult<EmployeeRoleLookupDto>.Fail(
                EmployeeAdministrationError.Validation,
                $"Tên quyền chỉ gồm chữ, số, dấu chấm, gạch dưới, gạch ngang và không vượt quá {EmployeeAdministrationRules.MaximumRoleNameLength} ký tự.");
        }

        var result = await _identityService.CreateRoleAsync(
            roleName,
            cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return EmployeeAdministrationResult<EmployeeRoleLookupDto>.Fail(
                EmployeeAdministrationError.Conflict,
                result.Error ?? "Không thể tạo loại quyền.");
        }

        return EmployeeAdministrationResult<EmployeeRoleLookupDto>.Ok(
            new EmployeeRoleLookupDto
            {
                RoleId = result.Data.RoleId,
                Name = result.Data.Name,
                IsPrivileged = EmployeeAdministrationRules.IsPrivilegedRole(result.Data.Name),
                ActiveAssignmentCount = 0
            });
    }
}
