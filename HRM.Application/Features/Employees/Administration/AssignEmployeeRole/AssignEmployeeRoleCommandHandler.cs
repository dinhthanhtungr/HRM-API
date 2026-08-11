using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Administration.GetEmployeeAccountPermissions;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Employees.Administration.AssignEmployeeRole;

internal sealed class AssignEmployeeRoleCommandHandler
    : IRequestHandler<
        AssignEmployeeRoleCommand,
        EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>
{
    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEmployeeIdentityAdministrationService _identityService;

    public AssignEmployeeRoleCommandHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser,
        IEmployeeIdentityAdministrationService identityService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>> Handle(
        AssignEmployeeRoleCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateTargetAndRoleAsync(
            request.EmployeeId,
            request.RoleName,
            cancellationToken);
        if (!validation.Success)
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                validation.Error,
                validation.Message!);
        }

        var result = await _identityService.AssignRoleAsync(
            request.EmployeeId,
            validation.RoleName!,
            cancellationToken);
        if (!result.Success)
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                EmployeeAdministrationError.Conflict,
                result.Error ?? "Không thể cấp quyền cho nhân viên.");
        }

        var account = await _identityService.GetAccountAsync(
            request.EmployeeId,
            cancellationToken);
        return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Ok(
            GetEmployeeAccountPermissionsQueryHandler.MapAccount(
                request.EmployeeId,
                true,
                account));
    }

    private async Task<RoleValidationResult> ValidateTargetAndRoleAsync(
        Guid employeeId,
        string requestedRoleName,
        CancellationToken cancellationToken)
    {
        if (!EmployeeAdministrationRules.CanManageEmployees(_currentUser))
        {
            return RoleValidationResult.Fail(
                EmployeeAdministrationError.Forbidden,
                "Bạn không có quyền cấp quyền nhân viên.");
        }

        var targetExists = await BuildEmployeeScope()
            .AnyAsync(
                employee =>
                    employee.EmployeeId == employeeId &&
                    employee.IsActive,
                cancellationToken);
        if (!targetExists)
        {
            return RoleValidationResult.Fail(
                EmployeeAdministrationError.NotFound,
                "Không tìm thấy nhân viên trong phạm vi được quản lý.");
        }

        var roleName = requestedRoleName.Trim();
        if (!EmployeeAdministrationRules.IsValidRoleName(roleName))
        {
            return RoleValidationResult.Fail(
                EmployeeAdministrationError.Validation,
                "Tên quyền không hợp lệ.");
        }

        var roles = await _identityService.GetRolesAsync(cancellationToken);
        var role = roles.FirstOrDefault(item =>
            string.Equals(item.Name, roleName, StringComparison.OrdinalIgnoreCase));
        if (role is null)
        {
            return RoleValidationResult.Fail(
                EmployeeAdministrationError.NotFound,
                "Loại quyền không tồn tại.");
        }

        if (EmployeeAdministrationRules.IsPrivilegedRole(role.Name) &&
            !EmployeeAdministrationRules.CanManageRoleTypes(_currentUser))
        {
            return RoleValidationResult.Fail(
                EmployeeAdministrationError.Forbidden,
                "Bạn không được cấp quyền đặc quyền.");
        }

        return RoleValidationResult.Ok(role.Name);
    }

    private IQueryable<HRM.Domain.Entities.HrSchema.Employee> BuildEmployeeScope()
    {
        var query = _dbContext.Employees.AsNoTracking();
        if (!EmployeeAdministrationRules.CanManageAllCompanies(_currentUser))
        {
            var companyId = _currentUser.CompanyId ?? Guid.Empty;
            query = query.Where(employee => employee.CompanyId == companyId);
        }

        return query;
    }

    private sealed record RoleValidationResult(
        bool Success,
        string? RoleName,
        EmployeeAdministrationError Error,
        string? Message)
    {
        public static RoleValidationResult Ok(string roleName)
            => new(true, roleName, EmployeeAdministrationError.None, null);

        public static RoleValidationResult Fail(
            EmployeeAdministrationError error,
            string message)
            => new(false, null, error, message);
    }
}
