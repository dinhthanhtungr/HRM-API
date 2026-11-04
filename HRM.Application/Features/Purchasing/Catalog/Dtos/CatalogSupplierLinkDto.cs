using HRM.Application.Features.PLM.Materials.Dtos;

namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogSupplierLinkDto
{
    public Guid MaterialsSupplierId { get; init; }
    public Guid SupplierId { get; init; }
    public string? SupplierCode { get; init; }
    public string? SupplierName { get; init; }
    public bool SupplierIsActive { get; init; }
    public bool IsActive { get; init; }
    public bool IsPreferred { get; init; }
    public decimal? CurrentPrice { get; init; }
    public CatalogLatestPriceDto LatestPrice { get; set; } = new();
    public string? Currency { get; init; }
    public int? MinDeliveryDays { get; init; }
    public SupplierActualDeliveryDto ActualDelivery { get; set; } = new();
    public DateTime? UpdatedDate { get; init; }
}
