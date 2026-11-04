using System.ComponentModel.DataAnnotations;

namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

internal sealed class CatalogMaterialAggregateRequest
{
    [Required] public CatalogMaterialWriteRequest? Material { get; init; }
    [Required, MaxLength(100)] public IReadOnlyList<CatalogSupplierPriceWriteRequest>? SupplierPrices { get; init; } = [];
}

public sealed class CatalogSupplierPriceWriteRequest
{
    internal const string UpdateOperation = "update";
    internal const string DeactivateOperation = "deactivate";
    [StringLength(20)] public string? Operation { get; init; } = UpdateOperation;
    internal bool IsDeactivation => Operation == DeactivateOperation;
    public Guid? MaterialsSupplierId { get; init; }
    public Guid? SupplierId { get; init; }
    public decimal? NewPrice { get; init; }
    public string? Currency { get; init; }
    public bool? IsPreferred { get; init; }
    public int? MinDeliveryDays { get; init; }
    public bool ClearMinDeliveryDays { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
    public decimal? ExpectedCurrentPrice { get; init; }
    public string? ExpectedCurrency { get; init; }
}

public sealed record CatalogSupplierPriceSavedDto(Guid MaterialsSupplierId, Guid SupplierId, decimal? CurrentPrice,
    string? Currency, DateTime UpdatedDate, Guid? PriceHistoryId, bool PriceChanged);

internal sealed record CatalogMaterialAggregateSavedDto(CatalogSavedDto Material, IReadOnlyList<CatalogSupplierPriceSavedDto> SupplierPrices);
