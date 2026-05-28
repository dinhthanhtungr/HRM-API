using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrderDetail;

public sealed record GetDeliveryOrderDetailQuery(Guid Id) : IRequest<DeliveryOrderDetailDto?>;
