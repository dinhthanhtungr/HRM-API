using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.EnsureDefaultAreas;

public sealed record EnsureInternalConversationAreasCommand(Guid ConversationId) : IRequest<OperationResult<EnsureInternalMailGroupsResultDto>>;
