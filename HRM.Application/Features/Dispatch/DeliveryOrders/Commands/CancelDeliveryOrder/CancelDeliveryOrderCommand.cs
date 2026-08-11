using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.CancelDeliveryOrder;

public sealed record CancelDeliveryOrderCommand(Guid DeliveryOrderId)
    : IRequest<OperationResult<DeliveryOrderLifecycleResultDto>>;
