using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Administration.GetEmployeeAccountPermissions;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace HRM.Application.Features.Employees.Administration.CreateEmployeeAccount;

internal sealed class CreateEmployeeAccountCommandHandler
    : IRequestHandler<
        CreateEmployeeAccountCommand,
        EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>
{
    private const int MaximumUserNameLength = 100;
    private const int MaximumEmailLength = 256;

    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEmployeeIdentityAdministrationService _identityService;

    public CreateEmployeeAccountCommandHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser,
        IEmployeeIdentityAdministrationService identityService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>> Handle(
        CreateEmployeeAccountCommand request,
        CancellationToken cancellationToken)
    {
        if (!EmployeeAdministrationRules.CanManageEmployees(_currentUser))
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                EmployeeAdministrationError.Forbidden,
                "Bạn không có quyền tạo tài khoản nhân viên.");
        }

        var userName = request.UserName.Trim();
        if (userName.Length is 0 or > MaximumUserNameLength)
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                EmployeeAdministrationError.Validation,
                $"Tên đăng nhập là bắt buộc và không vượt quá {MaximumUserNameLength} ký tự.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                EmployeeAdministrationError.Validation,
                "Mật khẩu là bắt buộc.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();
        if (email?.Length > MaximumEmailLength ||
            email is not null && !MailAddress.TryCreate(email, out _))
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                EmployeeAdministrationError.Validation,
                "Email không hợp lệ.");
        }

        var employee = await BuildEmployeeScope()
            .Where(item => item.EmployeeId == request.EmployeeId)
            .Select(item => new
            {
                item.EmployeeId,
                item.Email,
                item.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                EmployeeAdministrationError.NotFound,
                "Không tìm thấy nhân viên trong phạm vi được quản lý.");
        }

        if (!employee.IsActive)
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                EmployeeAdministrationError.Conflict,
                "Không thể tạo tài khoản cho nhân viên đã ngừng hoạt động.");
        }

        var accountResult = await _identityService.CreateAccountAsync(
            employee.EmployeeId,
            userName,
            email ?? employee.Email,
            request.Password,
            cancellationToken);
        if (!accountResult.Success || accountResult.Data is null)
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                EmployeeAdministrationError.Conflict,
                accountResult.Error ?? "Không thể tạo tài khoản nhân viên.");
        }

        return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Ok(
            GetEmployeeAccountPermissionsQueryHandler.MapAccount(
                employee.EmployeeId,
                employee.IsActive,
                accountResult.Data));
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
}
