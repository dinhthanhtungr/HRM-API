using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.GetEmployeeRoleLookup;

internal sealed class GetEmployeeRoleLookupQueryHandler
    : IRequestHandler<
        GetEmployeeRoleLookupQuery,
        EmployeeAdministrationResult<IReadOnlyList<EmployeeRoleLookupDto>>>
{
    private readonly IEmployeeIdentityAdministrationService _identityService;
    private readonly ICurrentUser _currentUser;

    public GetEmployeeRoleLookupQueryHandler(
        IEmployeeIdentityAdministrationService identityService,
        ICurrentUser currentUser)
    {
        _identityService = identityService;
        _currentUser = currentUser;
    }

    public async Task<EmployeeAdministrationResult<IReadOnlyList<EmployeeRoleLookupDto>>> Handle(
        GetEmployeeRoleLookupQuery request,
        CancellationToken cancellationToken)
    {
        if (!EmployeeAdministrationRules.CanManageEmployees(_currentUser))
        {
            return EmployeeAdministrationResult<IReadOnlyList<EmployeeRoleLookupDto>>.Fail(
                EmployeeAdministrationError.Forbidden,
                "Bạn không có quyền xem danh sách quyền.");
        }

        var canManagePrivilegedRoles =
            EmployeeAdministrationRules.CanManageRoleTypes(_currentUser);
        var canViewGlobalAssignmentCounts =
            EmployeeAdministrationRules.CanManageAllCompanies(_currentUser);
        var roles = await _identityService.GetRolesAsync(cancellationToken);

        var result = roles
            .Where(role =>
                canManagePrivilegedRoles ||
                !EmployeeAdministrationRules.IsPrivilegedRole(role.Name))
            .Select(role => new EmployeeRoleLookupDto
            {
                RoleId = role.RoleId,
                Name = role.Name,
                IsPrivileged = EmployeeAdministrationRules.IsPrivilegedRole(role.Name),
                ActiveAssignmentCount = canViewGlobalAssignmentCounts
                    ? role.ActiveAssignmentCount
                    : 0
            })
            .ToArray();

        return EmployeeAdministrationResult<IReadOnlyList<EmployeeRoleLookupDto>>.Ok(result);
    }
}
