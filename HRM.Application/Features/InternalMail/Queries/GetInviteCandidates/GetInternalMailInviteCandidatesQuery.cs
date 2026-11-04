using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetInviteCandidates;

public sealed record GetInternalMailInviteCandidatesQuery(Guid ConversationId) : IRequest<IReadOnlyList<InternalConversationParticipantDto>?>;
