using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.GetEmployeeRoleAccounts;

/// <summary>HR/President read company role assignments; Developer reads globally, including locked accounts and inactive employees.</summary>
public sealed class GetEmployeeRoleAccountsQuery : PaginationQuery,
    IRequest<EmployeeAdministrationResult<PagedResult<EmployeeRoleAccountDto>>>
{
    public Guid RoleId { get; set; }
}
