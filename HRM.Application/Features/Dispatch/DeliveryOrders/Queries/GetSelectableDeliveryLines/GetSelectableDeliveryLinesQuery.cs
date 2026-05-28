using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetSelectableDeliveryLines;

public sealed class GetSelectableDeliveryLinesQuery
    : PaginationQuery, IRequest<PagedResult<SelectableDeliveryOrderDto>>
{
    public Guid? CompanyId { get; init; }
    public Guid? CustomerId { get; init; }
}
