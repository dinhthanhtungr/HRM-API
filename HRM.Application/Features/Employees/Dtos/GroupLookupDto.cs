namespace HRM.Application.Features.Employees.Dtos;

public sealed class GroupLookupDto
{
    public Guid GroupId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? GroupType { get; init; }
    public Guid? CompanyId { get; init; }
}
