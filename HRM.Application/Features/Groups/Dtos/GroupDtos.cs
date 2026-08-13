namespace HRM.Application.Features.Groups.Dtos;

public sealed class GroupDto
{
    public Guid GroupId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? GroupType { get; init; }
    public Guid? PartId { get; init; }
    public int MemberCount { get; init; }
    public int LeaderCount { get; init; }
}

public sealed class GroupMemberDto
{
    public Guid MemberId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeExternalId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public bool IsLeader { get; init; }
    public bool IsActive { get; init; }
}

public sealed class GroupMembersDto
{
    public Guid GroupId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string? Name { get; init; }
    public IReadOnlyList<GroupMemberDto> Members { get; init; } = [];
}

public sealed class PartLookupDto
{
    public Guid PartId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string PartName { get; init; } = string.Empty;
}
