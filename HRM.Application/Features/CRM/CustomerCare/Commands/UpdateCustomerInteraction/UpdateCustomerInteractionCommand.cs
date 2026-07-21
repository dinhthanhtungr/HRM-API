using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.UpdateCustomerInteraction;

/// <summary>
/// Cập nhật một interaction trong customer visibility scope và đồng bộ WorkTask follow-up đang mở nếu có.
/// </summary>
public sealed class UpdateCustomerInteractionCommand : IRequest<OperationResult>
{
    public Guid InteractionId { get; init; }
    public UpdateCustomerInteractionRequest Request { get; init; } = new();
}
