using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.UpdateMessage;

public sealed class UpdateInternalMessageCommand : IRequest<OperationResult>
{
    public IReadOnlyList<Guid>? MentionedEmployeeIds { get; set; }
    public Guid MessageId { get; set; }
    public string? Body { get; set; }
    public bool? IsUrgent { get; set; }
}
