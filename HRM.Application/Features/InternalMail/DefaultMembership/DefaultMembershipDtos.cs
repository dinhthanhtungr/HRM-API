using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.DefaultMembership;

public sealed record DefaultMembershipRuleDto(DefaultMembershipRuleSource SourceType, Guid? RoleId,
    string? GroupTypeCode, DefaultMembershipCategoryScope CategoryScope, Guid? ProductCategoryId);

public sealed record PublishedDefaultMembershipRuleDto(Guid RuleId, DefaultMembershipRuleSource SourceType, Guid? RoleId,
    string? GroupTypeCode, DefaultMembershipCategoryScope CategoryScope, Guid? ProductCategoryId);

public sealed record DefaultMembershipRevisionDto(Guid RevisionId, int Version, Guid CreatedBy, DateTime CreatedAt,
    string Reason, Guid? RestoredFromRevisionId, bool IsActive, IReadOnlyList<PublishedDefaultMembershipRuleDto> Rules);

public sealed record DefaultMembershipPolicyDto(Guid? PolicyId, InternalConversationGroupType GroupType, int Version,
    Guid? ActiveRevisionId, bool Configured, IReadOnlyList<PublishedDefaultMembershipRuleDto> Rules);

public sealed class PublishDefaultMembershipRequest
{
    public int? ExpectedVersion { get; set; }
    public string? Reason { get; set; }
    public IReadOnlyList<DefaultMembershipRuleDto>? Rules { get; set; }
}

public sealed class RollbackDefaultMembershipRequest
{
    public int? ExpectedVersion { get; set; }
    public Guid RevisionId { get; set; }
    public string? Reason { get; set; }
}

public sealed record MembershipOption(Guid Id, string? Code, string? Name);
public sealed record DefaultMembershipOptions(IReadOnlyList<MembershipOption> Roles,
    IReadOnlyList<MembershipOption> ProductCategories, IReadOnlyList<string> LabGroupTypeCodes);

public enum MembershipRequestStatus { Success, Invalid, Forbidden, NotFound, Conflict }
public sealed record MembershipRequestResult<T>(MembershipRequestStatus Status, T? Value = default, string? ErrorCode = null);
