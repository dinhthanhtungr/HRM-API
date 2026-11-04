namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogLatestPriceDto
{
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public DateTime? PriceDate { get; init; }
    public string Source { get; init; } = "Unknown";
}
