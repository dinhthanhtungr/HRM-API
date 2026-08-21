using System.Text.Json.Serialization;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

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
