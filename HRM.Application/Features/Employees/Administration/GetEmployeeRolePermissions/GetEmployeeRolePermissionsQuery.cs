using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.GetEmployeeRolePermissions;

public sealed record GetEmployeeRolePermissionsQuery(Guid RoleId)
    : IRequest<EmployeeAdministrationResult<EmployeeRolePermissionsDto>>;
