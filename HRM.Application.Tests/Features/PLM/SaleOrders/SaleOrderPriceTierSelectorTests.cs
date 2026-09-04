using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.PLM.SaleOrders.Queries.GetLastSaleOrderByCustomer;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class SaleOrderPriceTierSelectorTests
{
    [Fact]
    public void Select_PrefersLatestCustomerQuotation()
    {
        var latestTier = Tier(120_000m);
        var references = References(
            approvedTiers: [Tier(110_000m)],
            systemTiers: [Tier(100_000m)],
            latestTiers: [latestTier]);

        var result = SaleOrderPriceTierSelector.Select(references);

        Assert.Equal(SaleOrderPriceTierSource.LatestCustomerQuotation, result.Source);
        Assert.Same(latestTier, Assert.Single(result.PriceTiers));
    }

    [Fact]
    public void Select_FallsBackFromApprovedToSystemCalculated()
    {
        var approved = SaleOrderPriceTierSelector.Select(References(
            approvedTiers: [Tier(110_000m)],
            systemTiers: [Tier(100_000m)],
            latestTiers: []));
        var system = SaleOrderPriceTierSelector.Select(References(
            approvedTiers: [],
            systemTiers: [Tier(100_000m)],
            latestTiers: []));

        Assert.Equal(SaleOrderPriceTierSource.ApprovedProductPricing, approved.Source);
        Assert.Equal(SaleOrderPriceTierSource.SystemCalculated, system.Source);
    }

    private static ResolvedProductTierPricingReferences References(
        IReadOnlyList<QuotationTierPriceReference> approvedTiers,
        IReadOnlyList<QuotationTierPriceReference> systemTiers,
        IReadOnlyList<QuotationTierPriceReference> latestTiers)
    {
        var productId = Guid.NewGuid();
        return new ResolvedProductTierPricingReferences(
            new ApprovedProductTierPricingReference(
                Guid.NewGuid(), productId, 1, ProductPricingStatus.Approved,
                new DateTime(2026, 9, 1), 110_000m,
                ProductPricingSourceType.Formula, Guid.NewGuid(), "VU1", "F001", approvedTiers),
            new SystemCalculatedTierPricingReference(
                productId, "Available", new DateTime(2026, 9, 2), 100_000m,
                ProductPricingSourceType.Formula, Guid.NewGuid(), "VU1", "F001", systemTiers),
            new LatestQuotedTierPricingReference(
                productId, Guid.NewGuid(), "BBG1", new DateTime(2026, 8, 30),
                new DateTime(2026, 8, 31), latestTiers),
            QuotationDefaultPriceTierSource.ApprovedPricingVersion);
    }

    private static QuotationTierPriceReference Tier(decimal unitPrice)
        => new("50 - 100", 50m, 100m, true, true, unitPrice, 0, new DateTime(2026, 9, 1));
}
