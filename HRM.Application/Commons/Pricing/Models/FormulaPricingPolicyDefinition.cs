using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Commons.Pricing.Models;

public sealed record FormulaPricingPolicyDefinition(
    FormulaPricingProfile Profile,
    decimal DefaultManufacturingCost,
    IReadOnlyList<FormulaPricingPolicyTierDefinition> Tiers);

public sealed record FormulaPricingPolicyTierDefinition(
    string QuantityRangeLabel,
    decimal? MinQuantity,
    decimal? MaxQuantity,
    bool MinInclusive,
    bool MaxInclusive,
    decimal? PriceOffset,
    int SortOrder);
