using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Employees.Administration.GetEmployeeAccountPermissions;

internal sealed class GetEmployeeAccountPermissionsQueryHandler
    : IRequestHandler<
        GetEmployeeAccountPermissionsQuery,
        EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>
{
    private readonly IEmployeeReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEmployeeIdentityAdministrationService _identityService;

    public GetEmployeeAccountPermissionsQueryHandler(
        IEmployeeReadDbContext dbContext,
        ICurrentUser currentUser,
        IEmployeeIdentityAdministrationService identityService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _identityService = identityService;
    }

    public async Task<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>> Handle(
        GetEmployeeAccountPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!EmployeeAdministrationRules.CanManageEmployees(_currentUser))
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                EmployeeAdministrationError.Forbidden,
                "Bạn không có quyền xem phân quyền nhân viên.");
        }

        var employeeExists = await BuildEmployeeScope()
            .AnyAsync(
                employee => employee.EmployeeId == request.EmployeeId,
                cancellationToken);
        if (!employeeExists)
        {
            return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Fail(
                EmployeeAdministrationError.NotFound,
                "Không tìm thấy nhân viên trong phạm vi được quản lý.");
        }

        var account = await _identityService.GetAccountAsync(
            request.EmployeeId,
            cancellationToken);

        return EmployeeAdministrationResult<EmployeeAccountPermissionsDto>.Ok(
            MapAccount(request.EmployeeId, account));
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

    internal static EmployeeAccountPermissionsDto MapAccount(
        Guid employeeId,
        EmployeeIdentityAccount? account)
        => new()
        {
            EmployeeId = employeeId,
            HasAccount = account is not null,
            UserId = account?.UserId,
            UserName = account?.UserName,
            Email = account?.Email,
            Roles = account?.ActiveRoles ?? []
        };
}
