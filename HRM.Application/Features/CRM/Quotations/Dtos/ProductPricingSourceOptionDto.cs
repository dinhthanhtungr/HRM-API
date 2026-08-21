using System.Text.Json.Serialization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

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

    // Legacy-named compatibility field resolved from latest item prices, not Formula.TotalPrice.
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
