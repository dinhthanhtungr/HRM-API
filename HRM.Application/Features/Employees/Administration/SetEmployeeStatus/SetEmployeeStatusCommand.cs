using HRM.Application.Features.Employees.Dtos;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.Employees.Administration.SetEmployeeStatus;

public sealed class SetEmployeeStatusCommand
    : IRequest<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>
{
    [JsonIgnore]
    public Guid EmployeeId { get; set; }

    public bool IsActive { get; init; }
}
