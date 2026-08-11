using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Administration.GetEmployeeAccountPermissions;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Employees.Administration.RevokeEmployeeRole;

internal sealed class RevokeEmployeeRoleCommandHandler
    : IRequestHandler<
        RevokeEmployeeRoleCommand,
        EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>
{
    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEmployeeIdentityAdministrationService _identityService;

    public RevokeEmployeeRoleCommandHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser,
        IEmployeeIdentityAdministrationService identityService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>> Handle(
        RevokeEmployeeRoleCommand request,
        CancellationToken cancellationToken)
    {
        if (!EmployeeAdministrationRules.CanManageEmployees(_currentUser))
        {
            return Fail(
                EmployeeAdministrationError.Forbidden,
                "Bạn không có quyền thu hồi quyền nhân viên.");
        }

        var targetExists = await BuildEmployeeScope()
            .AnyAsync(
                employee => employee.EmployeeId == request.EmployeeId,
                cancellationToken);
        if (!targetExists)
        {
            return Fail(
                EmployeeAdministrationError.NotFound,
                "Không tìm thấy nhân viên trong phạm vi được quản lý.");
        }

        var roleName = request.RoleName.Trim();
        if (!EmployeeAdministrationRules.IsValidRoleName(roleName))
        {
            return Fail(
                EmployeeAdministrationError.Validation,
                "Tên quyền không hợp lệ.");
        }

        var roles = await _identityService.GetRolesAsync(cancellationToken);
        var role = roles.FirstOrDefault(item =>
            string.Equals(item.Name, roleName, StringComparison.OrdinalIgnoreCase));
        if (role is null)
        {
            return Fail(
                EmployeeAdministrationError.NotFound,
                "Loại quyền không tồn tại.");
        }

        if (EmployeeAdministrationRules.IsPrivilegedRole(role.Name) &&
            !EmployeeAdministrationRules.CanManageRoleTypes(_currentUser))
        {
            return Fail(
                EmployeeAdministrationError.Forbidden,
                "Bạn không được thu hồi quyền đặc quyền.");
        }

        var account = await _identityService.GetAccountAsync(
            request.EmployeeId,
            cancellationToken);
        if (account is null)
        {
            return Fail(
                EmployeeAdministrationError.NotFound,
                "Nhân viên chưa có tài khoản.");
        }

        if (account.UserId == _currentUser.UserId &&
            EmployeeAdministrationRules.IsPrivilegedRole(role.Name) &&
            account.ActiveRoles.Count(EmployeeAdministrationRules.IsPrivilegedRole) <= 1)
        {
            return Fail(
                EmployeeAdministrationError.Forbidden,
                "Không thể tự thu hồi quyền quản trị cuối cùng của tài khoản hiện tại.");
        }

        var result = await _identityService.RevokeRoleAsync(
            request.EmployeeId,
            role.Name,
            cancellationToken);
        if (!result.Success)
        {
            return Fail(
                EmployeeAdministrationError.Conflict,
                result.Error ?? "Không thể thu hồi quyền nhân viên.");
        }

        var updatedAccount = await _identityService.GetAccountAsync(
            request.EmployeeId,
            cancellationToken);
        return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Ok(
            GetEmployeeAccountPermissionsQueryHandler.MapAccount(
                request.EmployeeId,
                updatedAccount));
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

    private static EmployeeAdministrationResult<EmployeeAccountPermissionsDto> Fail(
        EmployeeAdministrationError error,
        string message)
        => EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(error, message);
}
