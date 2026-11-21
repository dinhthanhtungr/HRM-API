using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Warehouse.Dtos;
using MediatR;

namespace HRM.Application.Features.Warehouse.Queries.GetStockMovements;

/// <summary>Reads persisted stock movements, including transfers and corrections, without unioning vouchers.</summary>
public sealed class GetStockMovementsQuery : PaginationQuery, IRequest<OperationResult<PagedResult<StockMovementDto>>>
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? BusinessType { get; init; }
    public string? Scope { get; init; }
}
