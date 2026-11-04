using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Internal structural metadata for cutover readers; never an authorization grant or public API entity.</summary>
internal sealed record InternalConversationGroupInfo(Guid ConversationId, Guid RootConversationId,
    InternalConversationGroupType GroupType, string? DisplayName);
