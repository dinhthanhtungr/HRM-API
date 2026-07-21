using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomerInteraction;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.ArchiveCustomerInteraction;

/// <summary>
/// Ngừng sử dụng interaction bằng soft delete, giữ lại lịch sử CRM để audit và báo cáo.
/// </summary>
public sealed record ArchiveCustomerInteractionCommand(Guid InteractionId) : IRequest<OperationResult>;

internal sealed class ArchiveCustomerInteractionCommandHandler
    : IRequestHandler<ArchiveCustomerInteractionCommand, OperationResult>
{
    private readonly ISender _sender;

    public ArchiveCustomerInteractionCommandHandler(ISender sender)
    {
        _sender = sender;
    }

    public Task<OperationResult> Handle(
        ArchiveCustomerInteractionCommand request,
        CancellationToken cancellationToken)
        => _sender.Send(new UpdateCustomerInteractionCommand
        {
            InteractionId = request.InteractionId,
            Request = new UpdateCustomerInteractionRequest { IsActive = false }
        }, cancellationToken);
}
