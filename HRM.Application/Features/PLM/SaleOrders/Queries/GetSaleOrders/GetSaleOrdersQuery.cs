using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetSaleOrders;

/// <summary>
/// Lấy danh sách SaleOrder active có phân trang trong công ty hiện tại, hỗ trợ tìm kiếm và lọc cơ bản,
/// bao gồm khoảng ngày tạo với hai đầu ngày được tính đầy đủ.
/// </summary>
public sealed class GetSaleOrdersQuery : PaginationQuery, IRequest<PagedResult<SaleOrderListItemDto>>
{
    public Guid? MerchandiseOrderId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? ManagerById { get; init; }
    public string? Status { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
