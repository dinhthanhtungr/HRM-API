namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogMaterialPriceHistoryDto
{
    public Guid PriceHistoryId { get; init; }
    public Guid MaterialsSupplierId { get; init; }
    public Guid SupplierId { get; init; }
    public string? SupplierCode { get; init; }
    public string? SupplierName { get; init; }
    public decimal? OldPrice { get; init; }
    public string? Currency { get; init; }
    public DateTime? RecordedAt { get; init; }
    public Guid? RecordedByEmployeeId { get; init; }
    public string? RecordedByName { get; init; }
}
