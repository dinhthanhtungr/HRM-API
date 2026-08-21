using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Administration.GetEmployeeAccountPermissions;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Employees.Administration.SetEmployeeStatus;

internal sealed class SetEmployeeStatusCommandHandler
    : IRequestHandler<SetEmployeeStatusCommand, EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>
{
    private readonly IEmployeeManagementDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEmployeeIdentityAdministrationService _identityService;

    public SetEmployeeStatusCommandHandler(
        IEmployeeManagementDbContext dbContext,
        ICurrentUser currentUser,
        IEmployeeIdentityAdministrationService identityService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>> Handle(
        SetEmployeeStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (!EmployeeAdministrationRules.CanManageEmployees(_currentUser))
        {
            return Fail(EmployeeAdministrationError.Forbidden,
                "Bạn không có quyền thay đổi trạng thái nhân viên.");
        }

        var employee = await BuildEmployeeScope()
            .FirstOrDefaultAsync(item => item.EmployeeId == request.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return Fail(EmployeeAdministrationError.NotFound,
                "Không tìm thấy nhân viên trong phạm vi được quản lý.");
        }

        if (!request.IsActive && employee.EmployeeId == _currentUser.EmployeeId)
        {
            return Fail(EmployeeAdministrationError.Forbidden,
                "Không thể tự ngừng hoạt động nhân viên đang đăng nhập.");
        }

        if (employee.IsActive != request.IsActive)
        {
            employee.IsActive = request.IsActive;
            employee.UpdatedBy = _currentUser.EmployeeId;
            employee.UpdatedDate = DateTime.Now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var account = await _identityService.GetAccountAsync(employee.EmployeeId, cancellationToken);
        if (account is not null &&
            EmployeeAccountLifecycleRules.MustDisableAccount(employee.IsActive) &&
            account.IsActive)
        {
            var disableResult = await _identityService.SetAccountActiveAsync(
                employee.EmployeeId,
                false,
                cancellationToken);
            if (!disableResult.Success)
            {
                return Fail(EmployeeAdministrationError.Conflict,
                    disableResult.Error ?? "Nhân viên đã ngừng hoạt động nhưng không thể vô hiệu hóa tài khoản.");
            }

            account = disableResult.Data;
        }

        return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Ok(
            GetEmployeeAccountPermissionsQueryHandler.MapAccount(
                employee.EmployeeId,
                employee.IsActive,
                account));
    }

    private IQueryable<HRM.Domain.Entities.HrSchema.Employee> BuildEmployeeScope()
    {
        var query = _dbContext.Employees.AsQueryable();
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
