namespace HRM.Application.Features.InternalMail.Reactions;

/// <summary>Tín hiệu làm mới cảm xúc; không phải notification và không chứa nội dung/người thả.</summary>
public static class InternalMessageReactionOutbox
{
    public const string Type = "InternalMailReactionChanged";
    public const string Event = "internalMailReactionChanged";
    public const string PersonalType = "InternalMailPersonalActivityChanged";
    public const string PersonalEvent = "internalMailPersonalActivityChanged";
}

public sealed record InternalMessageReactionEnvelope(Guid CompanyId, Guid ConversationId, Guid MessageId);
public sealed record InternalMailPersonalActivityEnvelope(Guid CompanyId, Guid ConversationId, Guid MessageId, Guid RecipientEmployeeId);
