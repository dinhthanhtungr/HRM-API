using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.SetReaction;

public sealed record SetInternalMessageReactionRequest(int ReactionType);
public sealed record SetInternalMessageReactionCommand(Guid ConversationId, Guid MessageId, int? ReactionType)
    : IRequest<OperationResult<InternalMessageReactionSummaryDto>>;
