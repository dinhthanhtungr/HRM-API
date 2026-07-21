using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Domain.Enums.WareHouses;
using MediatR;

namespace HRM.Application.Features.Warehouse.Queries.GetStockAvailable;

/// <summary>
/// Lấy danh sách tồn kho khả dụng cho màn hình Warehouse.
/// Query luôn loại kệ inactive trước khi group để tổng tồn, detail và available không bị sai.
/// </summary>
public sealed class GetStockAvailableQuery
    : PaginationQuery, IRequest<OperationResult<PagedResult<StockAvailableDto>>>
{
    public StockType? StockTypes { get; init; }

    public bool OnlyAvailableLeZero { get; init; }

    public decimal? AvailableMax { get; init; }
}
