using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrders;

public sealed class GetDeliveryOrdersQuery
    : PaginationQuery, IRequest<PagedResult<DeliveryOrderListItemDto>>
{
    public Guid? CompanyId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? DeliveryOrderId { get; init; }
    public string? Status { get; init; }
    public string? PONo { get; init; }
    public string? LotNo { get; init; }
    public bool? IsActive { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
