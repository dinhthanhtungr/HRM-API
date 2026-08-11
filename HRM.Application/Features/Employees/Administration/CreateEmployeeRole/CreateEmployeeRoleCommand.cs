using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.CreateEmployeeRole;

public sealed class CreateEmployeeRoleCommand
    : IRequest<EmployeeAdministrationResult<EmployeeRoleLookupDto>>
{
    public string Name { get; init; } = string.Empty;
}
