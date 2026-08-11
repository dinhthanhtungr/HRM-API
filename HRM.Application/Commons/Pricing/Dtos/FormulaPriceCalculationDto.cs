using System.Text.Json.Serialization;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Commons.Pricing.Dtos;

public sealed class FormulaPriceCalculationDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormulaPricingProfile Profile { get; init; }

    public decimal MaterialCost { get; init; }
    public decimal ManufacturingCost { get; init; }
    public bool UsedDefaultManufacturingCost { get; init; }
    public decimal CostBase { get; init; }

    public decimal StandardSellingPrice { get; init; }
    public decimal ProfitMarginRate { get; init; }

    public IReadOnlyList<FormulaSuggestedPriceTierDto> SuggestedPriceTiers { get; init; } = [];
}

public sealed class FormulaSuggestedPriceTierDto
{
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? MarginVsMaterialPercent { get; init; }
    public decimal? MarginVsCostPercent { get; init; }
    public bool RequiresManualPrice { get; init; }
    public int SortOrder { get; init; }
}
