using System.Text.Json.Serialization;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class QuotationResolvedProductPricingDto
{
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;

    public Guid FormulaId { get; init; }
    public string FormulaExternalId { get; init; } = string.Empty;
    public string FormulaName { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationFormulaSelectionSource FormulaSelectionSource { get; init; }

    public decimal? RealtimeMaterialCost { get; init; }
    public bool? IsRealtimeMaterialCostComplete { get; init; }
    public int? MissingMaterialPriceCount { get; init; }

    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public DateTime? PricingUpdatedDate { get; init; }

    public FormulaPriceCalculationDto? Pricing { get; init; }
}
