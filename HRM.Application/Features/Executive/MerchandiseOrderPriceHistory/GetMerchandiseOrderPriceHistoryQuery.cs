using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Executive.MerchandiseOrderPriceHistory.Dtos;
using MediatR;

namespace HRM.Application.Features.Executive.MerchandiseOrderPriceHistory;

public sealed class GetMerchandiseOrderPriceHistoryQuery
    : IRequest<OperationResult<PagedResult<MerchandiseOrderPriceHistoryItemDto>>>
{
    public Guid? ItemId { get; init; }
    public Guid? CustomerId { get; init; }
    public string? Currency { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }

    internal string? NormalizedCurrency => Normalize(Currency)?.ToUpperInvariant();
    internal string NormalizedSortBy => Normalize(SortBy) ?? "orderedAt";
    internal bool SortDescending => !string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
    internal int NormalizedPageNumber => PageNumber;
    internal int NormalizedPageSize => PageSize;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
