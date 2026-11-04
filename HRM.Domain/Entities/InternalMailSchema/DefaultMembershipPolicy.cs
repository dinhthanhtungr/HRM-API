using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Domain.Entities.InternalMailSchema;

/// <summary>Configuration only. Publishing never changes conversation membership.</summary>
public sealed class DefaultMembershipPolicy
{
    public Guid PolicyId { get; set; }
    public Guid CompanyId { get; set; }
    public InternalConversationGroupType GroupType { get; set; }
    public int Version { get; set; }
    public Guid? ActiveRevisionId { get; set; }
    public DefaultMembershipRevision? ActiveRevision { get; set; }
}
