using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Administration.GetEmployeeAccountPermissions;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Employees.Administration.SetEmployeeAccountStatus;

internal sealed class SetEmployeeAccountStatusCommandHandler
    : IRequestHandler<SetEmployeeAccountStatusCommand, EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>
{
    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEmployeeIdentityAdministrationService _identityService;

    public SetEmployeeAccountStatusCommandHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser,
        IEmployeeIdentityAdministrationService identityService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>> Handle(
        SetEmployeeAccountStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (!EmployeeAdministrationRules.CanManageEmployees(_currentUser))
        {
            return Fail(EmployeeAdministrationError.Forbidden,
                "Bạn không có quyền thay đổi trạng thái tài khoản nhân viên.");
        }

        var employee = await BuildEmployeeScope()
            .Where(item => item.EmployeeId == request.EmployeeId)
            .Select(item => new { item.EmployeeId, item.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return Fail(EmployeeAdministrationError.NotFound,
                "Không tìm thấy nhân viên trong phạm vi được quản lý.");
        }

        if (!request.IsActive && employee.EmployeeId == _currentUser.EmployeeId)
        {
            return Fail(EmployeeAdministrationError.Forbidden,
                "Không thể tự vô hiệu hóa tài khoản đang đăng nhập.");
        }

        if (request.IsActive &&
            !EmployeeAccountLifecycleRules.CanActivateAccount(employee.IsActive))
        {
            return Fail(EmployeeAdministrationError.Conflict,
                "Phải kích hoạt lại nhân viên trước khi kích hoạt tài khoản.");
        }

        var result = await _identityService.SetAccountActiveAsync(
            request.EmployeeId,
            request.IsActive,
            cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return Fail(EmployeeAdministrationError.Conflict,
                result.Error ?? "Không thể cập nhật trạng thái tài khoản.");
        }

        return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Ok(
            GetEmployeeAccountPermissionsQueryHandler.MapAccount(
                employee.EmployeeId,
                employee.IsActive,
                result.Data));
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
