using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationProductTierPricingReferencesTests
{
    [Fact]
    public void DefaultPriceTiers_UsesApprovedPricing_WhenApprovedTiersExist()
    {
        var approvedTiers = new[] { Tier(215_000m) };
        var systemTiers = new[] { Tier(218_000m) };
        var references = new ResolvedProductTierPricingReferences(
            Approved(approvedTiers),
            SystemCalculated(systemTiers),
            null,
            QuotationDefaultPriceTierSource.ApprovedPricingVersion);

        var tier = Assert.Single(references.DefaultPriceTiers);

        Assert.Equal(215_000m, tier.UnitPrice);
    }

    [Fact]
    public void DefaultPriceTiers_UsesSystemPricing_WhenApprovedPricingIsMissing()
    {
        var references = new ResolvedProductTierPricingReferences(
            null,
            SystemCalculated([Tier(0m)]),
            null,
            QuotationDefaultPriceTierSource.SystemCalculated);

        var tier = Assert.Single(references.DefaultPriceTiers);

        Assert.Equal(0m, tier.UnitPrice);
    }

    private static ApprovedProductTierPricingReference Approved(
        IReadOnlyList<QuotationTierPriceReference> tiers)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3,
            ProductPricingStatus.Approved,
            new DateTime(2026, 8, 20, 15, 52, 0),
            215_000m,
            ProductPricingSourceType.Formula,
            Guid.NewGuid(),
            "VU260600325",
            "F001",
            tiers);

    private static SystemCalculatedTierPricingReference SystemCalculated(
        IReadOnlyList<QuotationTierPriceReference> tiers)
        => new(
            Guid.NewGuid(),
            "Available",
            new DateTime(2026, 8, 23, 10, 30, 0),
            218_000m,
            ProductPricingSourceType.Formula,
            Guid.NewGuid(),
            "VU260600325",
            "F001",
            tiers);

    private static QuotationTierPriceReference Tier(decimal? unitPrice)
        => new(
            "< 50 kg",
            null,
            50m,
            true,
            false,
            unitPrice,
            0,
            null);
}
