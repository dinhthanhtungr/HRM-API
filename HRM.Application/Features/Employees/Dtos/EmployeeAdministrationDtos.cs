namespace HRM.Application.Features.Employees.Dtos;

public sealed class CompanyLookupDto
{
    public Guid CompanyId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class EmployeePartLookupDto
{
    public Guid PartId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string PartName { get; init; } = string.Empty;
}

public sealed class EmployeeAccountPermissionsDto
{
    public Guid EmployeeId { get; init; }
    public bool EmployeeIsActive { get; init; }
    public bool HasAccount { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public bool? AccountIsActive { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public sealed class EmployeeRoleLookupDto
{
    public Guid RoleId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsPrivileged { get; init; }
    public int ActiveAssignmentCount { get; init; }
}
