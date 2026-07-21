using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.DeleteMessage;

public sealed class DeleteInternalMessageCommand : IRequest<OperationResult>
{
    public Guid MessageId { get; init; }
}
