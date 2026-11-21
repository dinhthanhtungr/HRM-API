using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.Tracking;

public sealed record GetDeliveryTrackingQuery(Guid Id) : IRequest<DeliveryTrackingDetailDto?>;
