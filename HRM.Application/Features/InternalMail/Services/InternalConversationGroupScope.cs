using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Internal structural projection, never an authorization grant or a public API entity.</summary>
internal sealed class InternalConversationGroupScope
{
    public InternalConversation Conversation { get; init; } = null!;
    public Guid ConversationId { get; init; }
    public Guid RootConversationId { get; init; }
    public InternalConversationGroupType GroupType { get; init; }
    public string? DisplayName { get; init; }
}
