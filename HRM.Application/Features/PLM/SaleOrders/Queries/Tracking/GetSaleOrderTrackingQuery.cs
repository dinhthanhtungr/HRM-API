using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.Tracking;

public sealed record GetSaleOrderTrackingQuery(Guid OrderId) : IRequest<SaleOrderTrackingDetailDto?>;
