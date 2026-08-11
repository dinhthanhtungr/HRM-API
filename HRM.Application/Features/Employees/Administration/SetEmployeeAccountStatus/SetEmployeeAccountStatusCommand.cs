using HRM.Application.Features.Employees.Dtos;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.Employees.Administration.SetEmployeeAccountStatus;

public sealed class SetEmployeeAccountStatusCommand
    : IRequest<EmployeeAdministrationResult<EmployeeAccountPermissionsDto>>
{
    [JsonIgnore]
    public Guid EmployeeId { get; set; }

    public bool IsActive { get; init; }
}
