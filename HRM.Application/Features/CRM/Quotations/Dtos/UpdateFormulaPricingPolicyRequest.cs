using System.Text.Json.Serialization;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class UpdateFormulaPricingPolicyRequest
{
    public string Name { get; init; } = string.Empty;
    public decimal DefaultManufacturingCost { get; init; }
    public decimal DefaultProfitMarginRate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormulaPricingRoundingRule RoundingRule { get; init; }

    public decimal RoundingIncrement { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public IReadOnlyList<FormulaPricingPolicyTierRequest> Tiers { get; init; } = [];
    public DateTime? ExpectedUpdatedDate { get; init; }
}
