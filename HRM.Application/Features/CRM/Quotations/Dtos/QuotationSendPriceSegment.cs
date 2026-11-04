namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed record QuotationSendPriceSegment(Guid QuotationLineId, string ProductCode,
    Guid StandardPricingVersionId, DateTime StandardApprovedAt,
    decimal MinQuantity, decimal? MaxQuantity, bool MinInclusive, bool MaxInclusive,
    decimal StandardUnitPrice, decimal CustomerUnitPrice)
{
    public const string ApprovedTier = "ApprovedTier";
    public const string AdjacentTierFallback = "AdjacentTierFallback";
    public const string HighestApprovedTierFallback = "HighestApprovedTierFallback";
    public const string LastApprovedTierFallback = "LastApprovedTierFallback";
    /// <summary>Distinguishes an actual approved tier from a comparison-only fallback; does not create a stored tier.</summary>
    public string ComparisonBasis { get; init; } = ApprovedTier;
    public Guid CustomerPriceTierId { get; init; }
    public decimal? CustomerMinQuantity { get; init; }
    public decimal? CustomerMaxQuantity { get; init; }
    public bool CustomerMinInclusive { get; init; }
    public bool CustomerMaxInclusive { get; init; }
    /// <summary>Selected dominant-tier reference, or maximum reference across boundary/fallback segments.</summary>
    public decimal ReferenceUnitPrice { get; init; }
    public decimal ReferenceDifferenceAmount => CustomerUnitPrice - ReferenceUnitPrice;
    public bool IsBelowReference => ReferenceDifferenceAmount < 0m;
    public string Currency { get; init; } = string.Empty;
    public int StandardVersion { get; init; }
    public DateTime? StandardValidUntil { get; init; }
    public decimal DifferenceAmount => CustomerUnitPrice - StandardUnitPrice;
    public decimal? DifferencePercent => StandardUnitPrice > 0m
        ? decimal.Round(DifferenceAmount / StandardUnitPrice * 100m, 4, MidpointRounding.AwayFromZero) : null;
}
