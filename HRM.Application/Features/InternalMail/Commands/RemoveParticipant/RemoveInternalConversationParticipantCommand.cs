using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.RemoveParticipant;

public sealed class RemoveInternalConversationParticipantCommand : IRequest<OperationResult>
{
    public Guid ConversationId { get; init; }
    public Guid EmployeeId { get; init; }
}
