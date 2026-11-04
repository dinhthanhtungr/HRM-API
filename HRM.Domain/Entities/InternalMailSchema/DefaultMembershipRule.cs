using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Domain.Entities.InternalMailSchema;

public sealed class DefaultMembershipRule
{
    public Guid RuleId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid RevisionId { get; set; }
    public DefaultMembershipRuleSource SourceType { get; set; }
    public Guid? RoleId { get; set; }
    public string? GroupTypeCode { get; set; }
    public DefaultMembershipCategoryScope CategoryScope { get; set; }
    public Guid? ProductCategoryId { get; set; }
    public DefaultMembershipRevision Revision { get; set; } = null!;
}
