using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.Warehouse.Queries.GetWarehouseStockTracking;

public sealed record GetWarehouseStockTrackingQuery(string? Keyword, int PageNumber)
    : IRequest<PagedResult<WarehouseStockTrackingDto>?>;
