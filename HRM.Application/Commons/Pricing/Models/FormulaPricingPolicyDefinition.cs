using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Commons.Pricing.Models;

public sealed record FormulaPricingPolicyDefinition(
    FormulaPricingProfile Profile,
    decimal DefaultManufacturingCost,
    decimal DefaultProfitMarginRate,
    FormulaPricingRoundingRule RoundingRule,
    decimal RoundingIncrement,
    IReadOnlyList<FormulaPricingPolicyTierDefinition> Tiers);

public sealed record FormulaPricingPolicyTierDefinition(
    string QuantityRangeLabel,
    decimal? MinQuantity,
    decimal? MaxQuantity,
    bool MinInclusive,
    bool MaxInclusive,
    decimal? PriceOffset,
    int SortOrder);
