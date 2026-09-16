using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;
public sealed class GetPurchaseOrdersQuery : PaginationQuery, IRequest<PagedResult<PurchaseOrderListItemDto>>
{
    public string? Status { get; init; }
    public string? OrderType { get; init; }
    public Guid? SupplierId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
