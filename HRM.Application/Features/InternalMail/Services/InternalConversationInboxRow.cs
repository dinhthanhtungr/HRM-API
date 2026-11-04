using HRM.Domain.Entities.InternalMailSchema;

namespace HRM.Application.Features.InternalMail.Services;

internal sealed class InternalConversationInboxRow
{
    public Guid InternalConversationId { get; init; }
    public InternalConversation Conversation { get; init; } = null!;
    public DateTime ActivityAt { get; init; }
    public bool IsMember { get; init; }
    public bool IsArchived { get; init; }
    public bool IsMuted { get; init; }
}
