using System.Text.Json.Serialization;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class FormulaPricingPolicyTierRequest
{
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; } = true;
    public bool MaxInclusive { get; init; } = true;
    public decimal? PriceOffset { get; init; }
    public int SortOrder { get; init; }
}

public sealed class CreateFormulaPricingPolicyRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormulaPricingProfile Profile { get; init; }
    public string Currency { get; init; } = "VND";
    public string Name { get; init; } = string.Empty;
    public decimal DefaultManufacturingCost { get; init; }
    public decimal DefaultProfitMarginRate { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormulaPricingRoundingRule RoundingRule { get; init; }
    public decimal RoundingIncrement { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public IReadOnlyList<FormulaPricingPolicyTierRequest> Tiers { get; init; } = [];
}

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

public sealed class PublishFormulaPricingPolicyRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
}

public sealed class PreviewFormulaPricingPolicyRequest
{
    public decimal MaterialCost { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
}

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

public sealed class FormulaPricingPolicyTierDto
{
    public Guid FormulaPricingPolicyTierId { get; init; }
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal? PriceOffset { get; init; }
    public bool RequiresManualPrice => !PriceOffset.HasValue;
    public int SortOrder { get; init; }
}
