using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.ChangeStatus;

public sealed record ChangeDeliveryOrderStatusCommand(
    Guid DeliveryOrderId,
    string? Status) : IRequest<OperationResult<DeliveryOrderLifecycleResultDto>>;
