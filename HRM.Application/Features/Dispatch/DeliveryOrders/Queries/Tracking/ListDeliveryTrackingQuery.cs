using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.Tracking;

public sealed record ListDeliveryTrackingQuery(int PageNumber) : IRequest<PagedResult<DeliveryTrackingDto>?>;
