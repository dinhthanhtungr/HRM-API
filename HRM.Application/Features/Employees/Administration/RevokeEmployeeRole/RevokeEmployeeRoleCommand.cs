using HRM.Application.Features.Employees.Dtos;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.Employees.Administration.RevokeEmployeeRole;

public sealed class RevokeEmployeeRoleCommand
    : IRequest<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>
{
    [JsonIgnore]
    public Guid EmployeeId { get; set; }

    [JsonIgnore]
    public string RoleName { get; set; } = string.Empty;
}
