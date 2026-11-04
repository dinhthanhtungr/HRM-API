using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Queries.GetReactions;

public sealed record GetInternalMessageReactionSummaryQuery(Guid ConversationId, Guid MessageId)
    : IRequest<InternalMessageReactionSummaryDto?>;
