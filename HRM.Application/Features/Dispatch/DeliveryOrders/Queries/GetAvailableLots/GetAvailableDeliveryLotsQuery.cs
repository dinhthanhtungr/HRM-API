using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetAvailableLots;

public sealed record GetAvailableDeliveryLotsQuery(
    Guid MerchandiseOrderDetailId,
    Guid ProductId) : IRequest<OperationResult<IReadOnlyList<AvailableDeliveryLotDto>>>;
