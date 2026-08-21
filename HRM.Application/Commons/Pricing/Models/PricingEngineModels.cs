using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using HRM.Application.Commons.Pricing.Dtos;

namespace HRM.Application.Commons.Pricing.Models;

public sealed class PricingEngineRequest
{
    public Guid CompanyId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? SourceId { get; init; }
    public string? SourceType { get; init; }
    public FormulaPricingProfile? Profile { get; init; }
    public string Currency { get; init; } = string.Empty;
    public IReadOnlyList<FormulaMaterialCostItem> MaterialItems { get; init; } = [];
    public decimal? MaterialCost { get; init; }
    public decimal? ManufacturingCostOverride { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public ProductPricingChangedField? ChangedField { get; init; }
}

public sealed class PricingEngineResult
{
    public Guid FormulaPricingPolicyId { get; init; }
    public int FormulaPricingPolicyVersion { get; init; }
    public FormulaPricingProfile Profile { get; init; }
    public string Currency { get; init; } = string.Empty;
    public Guid? ProductId { get; init; }
    public Guid? SourceId { get; init; }
    public string? SourceType { get; init; }
    public bool IsMaterialCostComplete { get; init; }
    public int MissingMaterialPriceCount { get; init; }
    public decimal? MaterialCost { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? CostBase { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public IReadOnlyList<FormulaSuggestedPriceTierDto> SuggestedTiers { get; init; } = [];
    public FormulaPriceCalculationDto? Calculation { get; init; }
}
