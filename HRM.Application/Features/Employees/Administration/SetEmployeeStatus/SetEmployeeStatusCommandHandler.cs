using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
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

        var today = DateOnly.FromDateTime(DateTime.Today);
        var endDate = request.EndDate ?? today;
        if (!EmployeeAccountLifecycleRules.IsValidEndDate(request.IsActive, request.EndDate, employee.DateHired, today))
        {
            return Fail(EmployeeAdministrationError.Validation,
                "Ngày nghỉ phải từ ngày vào làm đến hôm nay; kích hoạt lại không nhận ngày nghỉ.");
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var wasActive = employee.IsActive;
        if (employee.IsActive != request.IsActive)
        {
            employee.IsActive = request.IsActive;
            employee.EndDate = request.IsActive ? null : endDate;
            employee.UpdatedBy = _currentUser.EmployeeId;
            employee.UpdatedDate = DateTime.Now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var account = await _identityService.GetAccountAsync(employee.EmployeeId, cancellationToken);
        if (account is not null &&
            (EmployeeAccountLifecycleRules.MustDisableAccount(employee.IsActive) || !wasActive))
        {
            var disableResult = await _identityService.SetAccountActiveAsync(
                employee.EmployeeId,
                false,
                cancellationToken);
            if (!disableResult.Success)
            {
                return Fail(EmployeeAdministrationError.Conflict,
                    disableResult.Error ?? "Không thể cập nhật nhân viên vì khóa tài khoản thất bại; thay đổi đã được hủy.");
            }

            account = disableResult.Data;
        }

        await transaction.CommitAsync(cancellationToken);
        return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Ok(
            EmployeeAccountPermissionsMapper.Map(
                employee.EmployeeId,
                employee.IsActive,
                account, employee.EndDate));
    }

    private IQueryable<HRM.Domain.Entities.HrSchema.Employee> BuildEmployeeScope()
        => EmployeeAdministrationRules.ScopeEmployees(_dbContext.Employees, _currentUser);

    private static EmployeeAdministrationResult<EmployeeAccountPermissionsDto> Fail(
        EmployeeAdministrationError error,
        string message)
        => EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(error, message);
}
