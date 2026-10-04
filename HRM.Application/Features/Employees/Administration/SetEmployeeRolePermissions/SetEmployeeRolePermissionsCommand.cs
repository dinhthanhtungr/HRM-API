using System.Text.Json.Serialization;
using HRM.Application.Features.Employees.Dtos;
using MediatR;

namespace HRM.Application.Features.Employees.Administration.SetEmployeeRolePermissions;

/// <summary>Thay toàn bộ permission đã biết; danh sách rỗng là thu hồi tất cả capability của role.</summary>
public sealed class SetEmployeeRolePermissionsCommand
    : IRequest<EmployeeAdministrationResult<EmployeeRolePermissionsDto>>
{
    [JsonIgnore]
    public Guid RoleId { get; set; }
    public string Version { get; init; } = string.Empty;
    public IReadOnlyList<string>? Permissions { get; init; }
}
