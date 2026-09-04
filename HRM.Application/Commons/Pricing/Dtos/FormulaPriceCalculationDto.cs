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
    public DateTime? PriceExpiresAt { get; set; }

    public IReadOnlyList<FormulaSuggestedPriceTierDto> SuggestedPriceTiers { get; init; } = [];
}
