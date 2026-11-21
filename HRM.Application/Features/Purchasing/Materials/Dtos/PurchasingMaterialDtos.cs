using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Application.Features.PLM.Materials.Dtos.PriceReview;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.Purchasing.Materials.Dtos;

public class PurchasingMaterialSummaryDto
{
    public bool IsSemiFinished { get; init; }
    public bool IsActive { get; init; }
    public Guid MaterialId { get; init; }
    public string? ExternalId { get; init; }
    public string? CustomCode { get; init; }
    public string? Name { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? Unit { get; init; }
    public string? Package { get; init; }
    public double? Weight { get; init; }
    public double? MinQuantity { get; init; }
    public int SupplierCount { get; init; }
    public CatalogLatestPriceDto? LatestPrice { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MaterialPriceReviewStatus? ReviewStatus { get; set; }
    public MaterialPurchaseAvailabilityInfoDto PurchaseAvailability { get; set; } = new();
}

public sealed class PurchasingMaterialDetailDto : PurchasingMaterialSummaryDto
{
    public IReadOnlyList<PurchasingMaterialSourceDto> SourceMaterials { get; set; } = [];
    public string? Barcode { get; init; }
    public string? Comment { get; init; }
    public DateTime? CreatedDate { get; init; }
    public Guid? CreatedBy { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public Guid? UpdatedBy { get; init; }
    public string? PurchaseAvailabilityNote { get; init; }
    public IReadOnlyList<string> GroupNames { get; init; } = [];
    public IReadOnlyList<PurchasingMaterialSupplierDto> Suppliers { get; init; } = [];
}

public sealed class PurchasingMaterialSourceDto
{
    public Guid MaterialId { get; init; }
    public string? ExternalId { get; init; }
    public string? Name { get; init; }
    public string? Unit { get; init; }
    public decimal Std { get; init; }
    public string FormulaCode { get; init; } = "";
}

public sealed class PurchasingMaterialSupplierDto
{
    public Guid MaterialsSupplierId { get; init; }
    public Guid SupplierId { get; init; }
    public string? SupplierCode { get; init; }
    public string? SupplierName { get; init; }
    public decimal? CurrentPrice { get; init; }
    public string? Currency { get; init; }
    public bool IsPreferred { get; init; }
    public int? MinDeliveryDays { get; init; }
    public DateTime? CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
}
