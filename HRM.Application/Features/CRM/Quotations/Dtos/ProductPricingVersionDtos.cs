using System.Text.Json.Serialization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class CreateProductPricingVersionRequest
{
    public Guid ProductId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingSourceType SourceType { get; init; }

    public Guid SourceId { get; init; }
    public string Currency { get; init; } = "VND";
    public bool ApproveImmediately { get; init; }
    public decimal? MaterialCostSnapshot { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingChangedField? ChangedField { get; init; }

    public DateTime? CalculatedAt { get; init; }
    public IReadOnlyList<ProductPricingTierRequest> PriceTiers { get; init; } = [];
}

public sealed class UpdateProductPricingVersionRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
    public decimal? MaterialCostSnapshot { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingChangedField? ChangedField { get; init; }

    public DateTime? CalculatedAt { get; init; }
    public IReadOnlyList<ProductPricingTierRequest> PriceTiers { get; init; } = [];
}

public sealed class ApproveProductPricingVersionRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
}

public sealed class ProductPricingTierRequest
{
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; } = true;
    public bool MaxInclusive { get; init; } = true;
    public decimal UnitPrice { get; init; }
    public int SortOrder { get; init; }
}

public sealed class ProductPricingVersionDto
{
    public Guid ProductPricingVersionId { get; init; }
    public Guid ProductId { get; init; }
    public Guid? FormulaPricingPolicyId { get; init; }
    public int? FormulaPricingPolicyVersion { get; init; }
    public bool? HasManualTierAdjustment { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingSourceType SourceType { get; init; }

    public Guid SourceId { get; init; }
    public string SourceExternalId { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public Guid? SourceFormulaId { get; init; }
    public Guid? SourceManufacturingFormulaId { get; init; }
    public Guid? SourceSampleTrialId { get; init; }
    public Guid? SourceManufacturingVUFormulaId { get; init; }
    public string? FormulaExternalIdSnapshot { get; init; }
    public string? BatchNoSnapshot { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal? MaterialCostSnapshot { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingStatus Status { get; init; }

    public int Version { get; init; }
    public DateTime? CalculatedAt { get; init; }
    public Guid? ApprovedBy { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public IReadOnlyList<ProductPricingTierDto> PriceTiers { get; init; } = [];
}

public sealed class ProductPricingSourceOptionDto
{
    public string PricingStatus { get; init; } = "Available";
    public Guid? FormulaPricingPolicyId { get; init; }
    public int? FormulaPricingPolicyVersion { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingSourceType SourceType { get; init; }

    public Guid SourceId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsEligible { get; init; }
    public bool IsCustomerSelected { get; init; }

    // Kept for backward compatibility with create/update pricing commands.
    // The value is resolved from the latest item prices, not Formula.TotalPrice.
    public decimal? MaterialCostSnapshot { get; init; }
    public decimal? CurrentMaterialCost { get; init; }
    public bool IsCurrentMaterialCostComplete { get; init; }
    public int MissingMaterialPriceCount { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public bool UsedDefaultManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormulaPricingProfile? PricingProfile { get; init; }

    public IReadOnlyList<FormulaSuggestedPriceTierDto> PriceTierTemplates { get; init; } = [];
    public FormulaPriceCalculationDto? Pricing { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public IReadOnlyList<QuotationProductPricingMaterialDto> Materials { get; init; } = [];
}

public sealed class ProductPricingTierDto
{
    public Guid ProductPricingTierId { get; init; }
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal UnitPrice { get; init; }
    public int SortOrder { get; init; }
}
