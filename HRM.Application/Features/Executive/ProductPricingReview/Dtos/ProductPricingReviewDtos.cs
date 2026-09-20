using System.Text.Json.Serialization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.CustomerEnum;
namespace HRM.Application.Features.Executive.ProductPricingReview.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PricingReviewSourceType
{
    VU = 0,
    VA = 10
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PricingReviewChangedField
{
    ManufacturingCost = 0,
    StandardSellingPrice = 10,
    ProfitMarginPercent = 20
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PricingReviewMaterialStatus
{
    UpToDate = 0,
    StalePrice = 10,
    MissingPrice = 20
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PricingReviewMaterialCategoryGroup
{
    Pigment = 0,
    Additive = 10,
    Resin = 20,
    Other = 30
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PricingReviewCostComparisonStatus
{
    Increased = 0,
    Decreased = 10,
    Unchanged = 20,
    Unavailable = 30
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PricingReviewMaterialPriceComparisonStatus
{
    Increased = 0,
    Decreased = 10,
    Unchanged = 20,
    MissingCurrentPrice = 30,
    MissingSourceSnapshot = 40
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PricingReviewFormulaMaterialComparisonStatus
{
    Unchanged = 0,
    QuantityChanged = 10,
    AddedToViewedFormula = 20,
    RemovedFromViewedFormula = 30,
    MissingCurrentPrice = 40,
    UnitMismatch = 50,
    Unavailable = 60
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PricingReviewFormulaComparisonUnavailableReason
{
    NoStandardFormula = 0,
    StandardSourceUnavailable = 10,
    MissingCurrentPrice = 20,
    UnitMismatch = 30
}

public sealed class ProductPricingReviewDto
{
    public PricingReviewHeaderDto Header { get; init; } = new();
    /// <summary>
    /// Pricing policy currently resolved for <see cref="SelectedSource"/>. Null when the selected
    /// source has no applicable published policy.
    /// </summary>
    public Guid? FormulaPricingPolicyId { get; init; }
    public PricingReviewSourceOptionDto? SelectedSource { get; init; }
    public PricingReviewCurrentFormulaUseDto? CurrentFormulaUse { get; init; }
    public PricingReviewOverviewDto Overview { get; init; } = new();
    public PricingReviewStandardPriceStateDto StandardPriceState { get; init; } = new();
    public StandardPriceRealtimeComparisonDto? RealtimePriceComparison { get; init; }
    public IReadOnlyList<PricingReviewMaterialDto> Materials { get; init; } = [];
    public PricingReviewEditorDto Editor { get; init; } = new();
    public PricingReviewTabCountsDto TabCounts { get; init; } = new();
}

/// <summary>
/// Derived state of the current standard price. It is deliberately not a
/// persisted request: a new Approved pricing version is the audit record that
/// resolves a pending reapproval.
/// </summary>
public sealed class PricingReviewStandardPriceStateDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductStandardPriceState State { get; init; }
    public bool RequiresPricingAction { get; init; }
    public bool HasFormulaConfirmationPending { get; init; }
    public bool IsReviewExpired { get; init; }
    public DateTime? PricingReviewDueDate { get; init; }
    public DateTime? LatestFormulaConfirmedAt { get; init; }
    public IReadOnlyList<ProductPricingAttentionSource> PricingAttentionSources { get; init; } = [];
}

public sealed class PricingReviewCurrentFormulaUseDto
{
    public PricingReviewSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceCode { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    /// <summary>Ghi chú nội bộ của chính công thức nguồn VU/VA; null khi nguồn không có ghi chú.</summary>
    public string? SourceNote { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsEligible { get; init; }
    public bool IsCurrentlyApplied { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed class PricingReviewHeaderDto
{
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public Guid? CustomerId { get; init; }
    public string? CustomerCode { get; init; }
    public string? CustomerName { get; init; }
    public string Currency { get; init; } = "VND";
    public DateTime? LastUpdatedAt { get; init; }
}

public sealed class PricingReviewSourceOptionDto
{
    public PricingReviewSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceCode { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    /// <summary>Ghi chú nội bộ của chính công thức nguồn VU/VA; chỉ có trên selectedSource của detail.</summary>
    public string? SourceNote { get; init; }
    public int? VersionNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsEligible { get; init; }
    public bool IsCurrentlyApplied { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public PricingReviewSourceMaterialPricingDto? MaterialPricing { get; init; }
}

public sealed class PricingReviewSourceMaterialPricingDto
{
    public int MaterialCount { get; init; }
    public decimal? CalculatedMaterialCost { get; init; }
    public decimal? BaselineMaterialCost { get; init; }
    public decimal? DifferenceAmount { get; init; }
    public decimal? DifferencePercent { get; init; }
    public PricingReviewCostComparisonStatus ComparisonStatus { get; init; }
    public int MissingPriceCount { get; init; }
    public int StalePriceCount { get; init; }
    public bool HasSourcePriceSnapshot { get; init; }
    public decimal? SourceSnapshotMaterialCost { get; init; }
    public bool CanCompare { get; init; }
}

public sealed class PricingReviewMaterialPricePreviewDto
{
    public PricingReviewMaterialPricePreviewSourceDto ViewedSource { get; init; } = new();
    public PricingReviewMaterialPricePreviewSourceDto? StandardSource { get; init; }
    public PricingReviewApprovedVersionReferenceDto? ApprovedPricingVersion { get; init; }
    public PricingReviewFormulaComparisonSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<PricingReviewFormulaMaterialComparisonDto> Materials { get; init; } = [];
    public int TotalCount { get; init; }
    public int ReturnedCount { get; init; }
}

public sealed class PricingReviewMaterialPricePreviewSourceDto
{
    public PricingReviewSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceCode { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int? VersionNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsSameAsViewedSource { get; init; }
}

public sealed class PricingReviewApprovedVersionReferenceDto
{
    public Guid PricingVersionId { get; init; }
    public int PricingVersionNumber { get; init; }
    public decimal? ApprovedMaterialCostSnapshot { get; init; }
    public string Currency { get; init; } = "VND";
}

public sealed class PricingReviewFormulaComparisonSummaryDto
{
    public string PriceBasis { get; init; } = "CurrentResolvedPrice";
    public DateTime CalculatedAt { get; init; }
    public decimal? StandardFormulaMaterialCost { get; init; }
    public decimal? ViewedFormulaMaterialCost { get; init; }
    public decimal? DifferenceAmount { get; init; }
    public decimal? DifferencePercent { get; init; }
    public PricingReviewCostComparisonStatus ComparisonStatus { get; init; }
    public int StandardMaterialCount { get; init; }
    public int ViewedMaterialCount { get; init; }
    public int MatchedMaterialCount { get; init; }
    public int AddedMaterialCount { get; init; }
    public int RemovedMaterialCount { get; init; }
    public int QuantityChangedCount { get; init; }
    public int UnchangedCount { get; init; }
    public int MissingPriceCount { get; init; }
    public int UnitMismatchCount { get; init; }
    public bool CanCompare { get; init; }
    public PricingReviewFormulaComparisonUnavailableReason? UnavailableReason { get; init; }
}

public sealed class PricingReviewFormulaCurrentPriceDto
{
    public decimal? UnitPrice { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LatestPriceSourceType PriceSource { get; init; }
    public DateTime? PriceDate { get; init; }
    /// <summary>Diễn giải giá realtime theo luật nội bộ; null khi chưa có giá hiện hành.</summary>
    public PriceCalculationDetailDto? Calculation { get; init; }
}

/// <summary>
/// Giá realtime của một NVL trên màn Executive Pricing Review. FE dùng
/// <c>price.calculation</c> để hiển thị tooltip cùng cấu trúc với màn Formula.
/// </summary>
public sealed class PricingReviewMaterialPriceDto
{
    public DateTime? LatestPriceDate { get; init; }
    public decimal? UnitPrice { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LatestPriceSourceType Source { get; init; } = LatestPriceSourceType.Unknown;

    public PriceCalculationDetailDto? Calculation { get; init; }
}

public sealed class PricingReviewFormulaMaterialSideDto
{
    public IReadOnlyList<Guid> FormulaMaterialIds { get; init; } = [];
    public bool IsPresent { get; init; }
    public decimal Quantity { get; init; }
    public decimal? Amount { get; init; }
}

public sealed class PricingReviewFormulaMaterialComparisonDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ItemType ItemType { get; init; }
    public Guid? MaterialId { get; init; }
    public Guid? ProductId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public string? CategoryCode { get; init; }
    public string? CategoryName { get; init; }
    public PricingReviewMaterialCategoryGroup CategoryGroup { get; init; }
    public string CategoryGroupName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public PricingReviewFormulaCurrentPriceDto CurrentPrice { get; init; } = new();
    public PricingReviewFormulaMaterialSideDto StandardFormula { get; init; } = new();
    public PricingReviewFormulaMaterialSideDto ViewedFormula { get; init; } = new();
    public decimal QuantityDifference { get; init; }
    public decimal? AmountDifference { get; init; }
    public decimal? DifferencePercent { get; init; }
    public PricingReviewFormulaMaterialComparisonStatus Status { get; init; }
}

public sealed class PricingReviewOverviewDto
{
    public Guid? PricingVersionId { get; init; }
    public int? PricingVersionNumber { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? MaterialCost { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? ProfitAmount { get; init; }
    public decimal? ProfitMarginPercent { get; init; }
    public string? PublisherNote { get; init; }
    public int TotalMaterialCount { get; init; }
    public int ReviewRequiredCount { get; init; }
    public int MissingPriceCount { get; init; }
    public int StalePriceCount { get; init; }
}

public sealed class PricingReviewMaterialDto
{
    public Guid FormulaMaterialId { get; init; }
    public Guid? MaterialId { get; init; }
    public Guid? ProductId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public PricingReviewMaterialCategoryGroup CategoryGroup { get; init; }
    public string CategoryGroupName { get; init; } = string.Empty;
    public string? MaterialType { get; init; }
    public string? GroupName { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal? CurrentUnitPrice { get; init; }
    public decimal? MaterialAmount { get; init; }
    public DateTime? PriceDate { get; init; }
    public string? PriceSource { get; init; }
    /// <summary>
    /// Giá realtime và diễn giải luật tính giá. Field giá phẳng phía trên giữ lại
    /// tương thích cho FE cũ.
    /// </summary>
    public PricingReviewMaterialPriceDto Price { get; init; } = new();
    public PricingReviewMaterialStatus Status { get; init; }
}

public sealed class PricingReviewEditorDto
{
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginPercent { get; init; }
    public string? PublisherNote { get; init; }
    public string? InternalNote { get; init; }
    public IReadOnlyList<PricingReviewPriceTierDto> PriceTiers { get; init; } = [];
    public DateTime? ExpectedUpdatedAt { get; init; }
}

public sealed class PricingReviewTabCountsDto
{
    public int PricingHistoryCount { get; init; }
    public int RelatedQuotationCount { get; init; }
    public int VaLotCount { get; init; }
}

public sealed class PricingReviewSupplierPricesDto
{
    public Guid MaterialId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public IReadOnlyList<PricingReviewSupplierPriceDto> Suppliers { get; init; } = [];
}

public sealed class PricingReviewSupplierPriceDto
{
    public Guid SupplierPriceId { get; init; }
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public decimal? UnitPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime? PriceDate { get; init; }
    public bool IsCurrentStandardPrice { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class PricingReviewMaterialPriceHistoryDto
{
    public Guid PriceHistoryId { get; init; }
    public Guid SupplierPriceId { get; init; }
    public Guid SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public decimal? UnitPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime? RecordedAt { get; init; }
    public Guid? RecordedByEmployeeId { get; init; }
    public string? RecordedByName { get; init; }
}

public class PricingReviewPreviewRequest
{
    public string Currency { get; init; } = "VND";
    public PricingReviewSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public IReadOnlyList<PricingReviewMaterialPriceSelectionRequest> MaterialPriceSelections { get; init; } = [];
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginPercent { get; init; }
    public PricingReviewChangedField? ChangedField { get; init; }
    public IReadOnlyList<PricingReviewPriceTierRequest> PriceTiers { get; init; } = [];
}

public sealed class PricingReviewMaterialPriceSelectionRequest
{
    public Guid FormulaMaterialId { get; init; }
    public Guid MaterialId { get; init; }
    public Guid SupplierPriceId { get; init; }
    public decimal UnitPrice { get; init; }
    public DateTime? ExpectedUpdatedAt { get; init; }
}

public sealed class PricingReviewPriceTierRequest
{
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; } = true;
    public bool MaxInclusive { get; init; } = true;
    public decimal UnitPrice { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class PricingReviewPreviewDto
{
    public PricingReviewSourceOptionDto Source { get; init; } = new();
    public decimal MaterialCost { get; init; }
    public decimal ManufacturingCost { get; init; }
    public decimal CostBase { get; init; }
    public decimal StandardSellingPrice { get; init; }
    public decimal ProfitAmount { get; init; }
    public decimal ProfitMarginPercent { get; init; }
    public int MissingPriceCount { get; init; }
    public IReadOnlyList<PricingReviewPriceTierDto> PriceTiers { get; init; } = [];
}

public sealed class PricingReviewPriceTierDto
{
    public Guid? PricingTierId { get; init; }
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal? UnitPrice { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
    public bool RequiresManualPrice { get; init; }
    public bool IsStored { get; init; }
}

public sealed class PricingReviewVersionDto
{
    public Guid PricingVersionId { get; init; }
    public int VersionNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public PricingReviewSourceType? SourceType { get; init; }
    public Guid? SourceId { get; init; }
    public string? SourceCode { get; init; }
    public decimal? MaterialCost { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public StandardPriceRealtimeComparisonDto? RealtimePriceComparison { get; init; }
    public decimal? ProfitAmount { get; init; }
    public decimal? ProfitMarginPercent { get; init; }
    public string? PublisherNote { get; init; }
    public string? InternalNote { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public Guid CreatedByEmployeeId { get; init; }
    public string? CreatedByName { get; init; }
    public Guid? UpdatedByEmployeeId { get; init; }
    public string? UpdatedByName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public IReadOnlyList<PricingReviewPriceTierDto> PriceTiers { get; init; } = [];
}

public sealed class PricingReviewRelatedQuotationDto
{
    public Guid QuotationId { get; init; }
    public string QuotationCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime QuotationDate { get; init; }
}

public sealed class PricingReviewVaLotDto
{
    public Guid ManufacturingFormulaId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int? VersionNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsCurrentlyApplied { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
}

public sealed class CreatePricingReviewVersionRequest : PricingReviewPreviewRequest
{
    public int? SourceVersionNumber { get; init; }
    public string? PublisherNote { get; init; }
    public string? InternalNote { get; init; }
    public bool ApproveImmediately { get; init; }
    public Guid? ExpectedCurrentVersionId { get; init; }
    public DateTime? ExpectedCurrentVersionUpdatedAt { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
}

public sealed class UpdatePricingReviewVersionRequest : PricingReviewPreviewRequest
{
    public int? SourceVersionNumber { get; init; }
    public string? PublisherNote { get; init; }
    public string? InternalNote { get; init; }
    public DateTime? ExpectedUpdatedAt { get; init; }
}

public sealed class ApprovePricingReviewVersionRequest
{
    public DateTime? ExpectedUpdatedAt { get; init; }
    public string? PublisherNote { get; init; }
    public string? InternalNote { get; init; }
}

public sealed class ConfirmCurrentStandardPriceRequest
{
    /// <summary>Client-generated GUID. Retrying the same request is idempotent.</summary>
    public string IdempotencyKey { get; init; } = string.Empty;
    public Guid? ExpectedApprovedPricingVersionId { get; init; }
    public DateTime? ExpectedApprovedPricingVersionUpdatedAt { get; init; }
    public string? InternalNote { get; init; }
}
