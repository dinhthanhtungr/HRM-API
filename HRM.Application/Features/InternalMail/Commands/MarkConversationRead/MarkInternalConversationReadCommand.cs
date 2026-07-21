using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.MarkConversationRead;

public sealed class MarkInternalConversationReadCommand : IRequest<OperationResult>
{
    public Guid ConversationId { get; init; }
}
