using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Domain.Enums.WareHouses;
using MediatR;

namespace HRM.Application.Features.Warehouse.Queries.GetStockAvailable;

/// <summary>
/// Lấy danh sách tồn kho khả dụng cho màn hình Warehouse.
/// Hiển thị cả kệ inactive; chỉ tồn trên kệ active được tính vào tổng cũ và available.
/// </summary>
public sealed class GetStockAvailableQuery
    : PaginationQuery, IRequest<OperationResult<PagedResult<StockAvailableDto>>>
{
    internal bool ExportAll { get; init; }

    public StockType[] StockTypes { get; init; } = [];

    public bool OnlyAvailableLeZero { get; init; }

    public decimal? AvailableMax { get; init; }

    // Internal batch consumers only; controllers do not bind this property.
    internal string[]? ExactCodes { get; init; }
}
