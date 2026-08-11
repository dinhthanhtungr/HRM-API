using HRM.Application.Features.Employees.Dtos;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.Employees.Administration.AssignEmployeeRole;

public sealed class AssignEmployeeRoleCommand
    : IRequest<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>
{
    [JsonIgnore]
    public Guid EmployeeId { get; set; }

    public string RoleName { get; init; } = string.Empty;
}
