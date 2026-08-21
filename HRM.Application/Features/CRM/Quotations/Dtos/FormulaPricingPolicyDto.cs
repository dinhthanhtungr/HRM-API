using System.Text.Json.Serialization;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class FormulaPricingPolicyDto
{
    public Guid FormulaPricingPolicyId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormulaPricingProfile Profile { get; init; }

    public string Currency { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Version { get; init; }
    public decimal DefaultManufacturingCost { get; init; }
    public decimal DefaultProfitMarginRate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormulaPricingRoundingRule RoundingRule { get; init; }

    public decimal RoundingIncrement { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormulaPricingPolicyStatus Status { get; init; }

    public DateTime? EffectiveFrom { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public IReadOnlyList<FormulaPricingPolicyTierDto> Tiers { get; init; } = [];
}
