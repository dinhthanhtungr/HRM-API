using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Domain.Entities.InternalMailSchema;

/// <summary>
/// One structural mapping per existing conversation. Application persistence maintains writes atomically;
/// membership, activity and business-reference fields remain on the existing entities.
/// </summary>
public sealed class InternalConversationGroup
{
    public Guid ConversationId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid RootConversationId { get; set; }
    public InternalConversationGroupType GroupType { get; set; }
    /// <summary>Null means the localized default label. Custom groups require a nonblank name.</summary>
    public string? DisplayName { get; set; }
    /// <summary>Index mirror synchronized from InternalConversation.IsActive, never a separate grant.</summary>
    public bool IsActive { get; set; }
    public InternalConversation Conversation { get; set; } = default!;
    public InternalConversationGroup RootGroup { get; set; } = default!;
}
