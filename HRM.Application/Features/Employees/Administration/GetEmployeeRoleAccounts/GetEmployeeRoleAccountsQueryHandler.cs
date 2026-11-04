using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.GetEmployeeRoleAccounts;

internal sealed class GetEmployeeRoleAccountsQueryHandler(
    ICurrentUser currentUser, IEmployeeIdentityAdministrationService service)
    : IRequestHandler<GetEmployeeRoleAccountsQuery, EmployeeAdministrationResult<PagedResult<EmployeeRoleAccountDto>>>
{
    public async Task<EmployeeAdministrationResult<PagedResult<EmployeeRoleAccountDto>>> Handle(
        GetEmployeeRoleAccountsQuery request, CancellationToken cancellationToken)
    {
        var global = EmployeeAdministrationRules.CanManageAllCompanies(currentUser);
        if (!EmployeeRoleAccountAccess.CanView(currentUser) || (!global && !currentUser.CompanyId.HasValue))
            return EmployeeAdministrationResult<PagedResult<EmployeeRoleAccountDto>>.Fail(
                EmployeeAdministrationError.Forbidden, "Bạn không có quyền xem tài khoản được cấp role trong phạm vi này.");

        if (request.RoleId == Guid.Empty || request.NormalizedKeyword?.Length > 200 ||
            (long)(request.NormalizedPageNumber - 1) * request.NormalizedPageSize > int.MaxValue)
            return EmployeeAdministrationResult<PagedResult<EmployeeRoleAccountDto>>.Fail(
                EmployeeAdministrationError.Validation, "Role hoặc tham số phân trang/tìm kiếm không hợp lệ.");

        var companyId = global ? null : currentUser.CompanyId;
        var roles = await service.GetRolesAsync(cancellationToken, companyId);
        var role = roles.FirstOrDefault(item => item.RoleId == request.RoleId);
        if (role is not null && EmployeeAdministrationRules.IsPrivilegedRole(role.Name) &&
            !EmployeeAdministrationRules.CanManageRoleTypes(currentUser))
            return EmployeeAdministrationResult<PagedResult<EmployeeRoleAccountDto>>.Fail(
                EmployeeAdministrationError.Forbidden, "Bạn không có quyền xem role đặc quyền.");

        var result = await service.GetRoleAccountsAsync(request.RoleId, request.NormalizedPageNumber,
            request.NormalizedPageSize, request.NormalizedKeyword, cancellationToken, companyId);
        return result is null
            ? EmployeeAdministrationResult<PagedResult<EmployeeRoleAccountDto>>.Fail(
                EmployeeAdministrationError.NotFound, "Không tìm thấy role.")
            : EmployeeAdministrationResult<PagedResult<EmployeeRoleAccountDto>>.Ok(result);
    }
}
