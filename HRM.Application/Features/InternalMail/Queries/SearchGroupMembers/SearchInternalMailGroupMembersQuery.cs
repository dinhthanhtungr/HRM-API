using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.SearchGroupMembers;

/// <summary>Company-scoped employee lookup for creating an empty private group; grants no source access.</summary>
public sealed record SearchInternalMailGroupMembersQuery(Guid ConversationId, string? Keyword)
    : IRequest<IReadOnlyList<InternalConversationParticipantDto>?>;
