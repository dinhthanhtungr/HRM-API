using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationSendPriceComparisonTests
{
    [Theory]
    [InlineData(50, 100, true, 4.62)]
    [InlineData(40, 60, true, 5.59)]
    [InlineData(50, 50, true, 5.59)]
    [InlineData(50, 51, true, 5.59)]
    [InlineData(50, 100, false, 4.62)]
    [InlineData(50, null, true, 4.62)]
    [InlineData(101, null, true, 4.62)]
    [InlineData(0, 24, true, 5.59)]
    public void DominantRegionMatchesFrontendExamples(decimal min, int? max, bool inclusive, decimal expected)
    {
        var (line, standard) = Setup(min, max, expected);
        line.PriceTiers.Single().MinInclusive = inclusive;
        standard.PriceTiers.Add(Tier(25, 50, 5.59m));
        standard.PriceTiers.Add(Tier(51, max is null && min == 50 ? null : 100, 4.62m));
        var rows = QuotationSendPriceComparison.Compare(line, standard);
        Assert.All(rows, row => Assert.Equal(expected, row.ReferenceUnitPrice));
        Assert.All(rows, row => Assert.False(row.IsBelowReference));
        Assert.All(rows, row => Assert.Equal(line.PriceTiers.Single().QuotationLinePriceTierId, row.CustomerPriceTierId));
    }

    [Fact]
    public void EqualOverlapLengthsUseHigherPriceAndPreserveTierIdentity()
    {
        var (line, standard) = Setup(25, 75, 100);
        line.PriceTiers.Single().QuotationLinePriceTierId = Guid.NewGuid();
        standard.PriceTiers.Add(Tier(0, 50, 100));
        standard.PriceTiers.Add(Tier(50, 100, 50, minInclusive: false));
        Assert.All(QuotationSendPriceComparison.Compare(line, standard), row =>
        {
            Assert.Equal(100, row.ReferenceUnitPrice);
            Assert.Equal(line.PriceTiers.Single().QuotationLinePriceTierId, row.CustomerPriceTierId);
        });
    }

    [Fact]
    public void UnboundedLowerIntersectionMatchesFrontendInfinitySemantics()
    {
        var (line, standard) = Setup(0, 100, 100);
        line.PriceTiers.Single().MinQuantity = null;
        standard.PriceTiers.Add(new ProductPricingTier { MinQuantity = null, MaxQuantity = 0, UnitPrice = 100 });
        standard.PriceTiers.Add(Tier(0, 100, 50, minInclusive: false));
        Assert.All(QuotationSendPriceComparison.Compare(line, standard), row => Assert.Equal(100, row.ReferenceUnitPrice));
    }

    [Fact]
    public void CustomerSubsetUsesCoveringStandardAndIncludesCommission()
    {
        var (line, standard) = Setup(350, 600, 90, 10);
        standard.PriceTiers.Add(Tier(300, 600, 100));
        var segment = Assert.Single(QuotationSendPriceComparison.Compare(line, standard));
        Assert.Equal(350, segment.MinQuantity);
        Assert.Equal(600, segment.MaxQuantity);
        Assert.Equal(0, segment.DifferenceAmount);
    }

    [Fact]
    public void CrossingTiersDetectsAnyBelowStandardPortion()
    {
        var (line, standard) = Setup(350, 600, 95);
        standard.PriceTiers.Add(Tier(300, 500, 100, maxInclusive: false));
        standard.PriceTiers.Add(Tier(500, 600, 90));
        var segments = QuotationSendPriceComparison.Compare(line, standard);
        Assert.Single(segments);
        Assert.Equal(-5, segments[0].DifferenceAmount);
        Assert.Equal(-5, segments[0].DifferencePercent);
        Assert.Equal(100, segments[0].ReferenceUnitPrice);
    }

    [Fact]
    public void GapAtSingleBoundaryUsesHigherAdjacentPrice()
    {
        var (line, standard) = Setup(500, 500, 95);
        standard.PriceTiers.Add(Tier(300, 500, 100, maxInclusive: false));
        standard.PriceTiers.Add(Tier(500, 600, 90, minInclusive: false));
        var gap = Assert.Single(QuotationSendPriceComparison.Compare(line, standard),
            x => x.ComparisonBasis == QuotationSendPriceSegment.AdjacentTierFallback);
        Assert.Equal(500, gap.MinQuantity);
        Assert.Equal(500, gap.MaxQuantity);
        Assert.True(gap.MinInclusive && gap.MaxInclusive);
        Assert.Equal(100, gap.StandardUnitPrice);
    }

    [Fact]
    public void OverlappingApprovedBoundaryStillRejectsAmbiguousStandard()
    {
        var (line, standard) = Setup(350, 600, 95);
        standard.PriceTiers.Add(Tier(300, 500, 100));
        standard.PriceTiers.Add(Tier(500, 600, 90));
        Assert.Throws<InvalidOperationException>(() => QuotationSendPriceComparison.Compare(line, standard));
    }

    [Fact]
    public void InteriorGapUsesAdjacentPricesRatherThanUnrelatedHigherTier()
    {
        var (line, standard) = Setup(400, 500, 95);
        standard.PriceTiers.Add(Tier(300, 400, 100));
        standard.PriceTiers.Add(Tier(500, 600, 90));
        standard.PriceTiers.Add(Tier(700, 800, 1000));
        var gap = Assert.Single(QuotationSendPriceComparison.Compare(line, standard),
            x => x.ComparisonBasis == QuotationSendPriceSegment.AdjacentTierFallback);
        Assert.Equal(400, gap.MinQuantity);
        Assert.Equal(500, gap.MaxQuantity);
        Assert.False(gap.MinInclusive || gap.MaxInclusive);
        Assert.Equal(100, gap.StandardUnitPrice);
        Assert.Equal(-5, gap.DifferenceAmount);
    }

    [Fact]
    public void UnboundedCustomerRangeUsesLastPriceBeyondApprovedTable()
    {
        var (line, standard) = Setup(350, null, 95);
        standard.PriceTiers.Add(Tier(100, 200, 150));
        standard.PriceTiers.Add(Tier(300, 600, 100));
        var dominant = Assert.Single(QuotationSendPriceComparison.Compare(line, standard));
        Assert.Equal(100, dominant.StandardUnitPrice);
        Assert.Equal(QuotationSendPriceSegment.ApprovedTier, dominant.ComparisonBasis);
        line.PriceTiers.Single().MinQuantity = 700;
        var outside = Assert.Single(QuotationSendPriceComparison.Compare(line, standard));
        Assert.Equal(100, outside.StandardUnitPrice);
        Assert.Equal(QuotationSendPriceSegment.LastApprovedTierFallback, outside.ComparisonBasis);
        line.PriceTiers.Single().MinQuantity = 350;
        standard.PriceTiers.Clear();
        standard.PriceTiers.Add(Tier(300, null, 100));
        Assert.Equal(QuotationSendPriceSegment.ApprovedTier,
            Assert.Single(QuotationSendPriceComparison.Compare(line, standard)).ComparisonBasis);
    }

    [Fact]
    public void BeforeApprovedTableUsesGlobalHighestInsteadOfFirstTier()
    {
        var (line, standard) = Setup(100, 200, 95);
        standard.PriceTiers.Add(Tier(300, 600, 100));
        standard.PriceTiers.Add(Tier(700, 800, 150));
        var segment = Assert.Single(QuotationSendPriceComparison.Compare(line, standard));
        Assert.Equal(150, segment.StandardUnitPrice);
        Assert.Equal(QuotationSendPriceSegment.HighestApprovedTierFallback, segment.ComparisonBasis);
    }

    [Theory]
    [InlineData(20000, null)]
    [InlineData(10025, 20000)]
    [InlineData(7000, null)]
    public void LastQuantityTierMatchesNewFrontendExamples(decimal min, int? max)
    {
        var (line, standard) = Setup(min, max, 29000);
        // Intentionally unsorted: last means quantity endpoint, not insertion order or highest price.
        standard.PriceTiers.Add(Tier(6025, 10000, 29400));
        standard.PriceTiers.Add(Tier(1025, 3000, 30200));
        standard.PriceTiers.Add(Tier(3025, 6000, 29800));
        standard.PriceTiers.Add(new ProductPricingTier
        {
            MinQuantity = 10001, MaxQuantity = 99999, UnitPrice = 99999, IsActive = false
        });

        var row = Assert.Single(QuotationSendPriceComparison.Compare(line, standard));
        Assert.Equal(29400, row.ReferenceUnitPrice);
        Assert.Equal(-400, row.DifferenceAmount);
        Assert.Equal(-1.3605m, row.DifferencePercent);
        Assert.True(row.IsBelowReference);
        Assert.Equal(min >= 10000 ? QuotationSendPriceSegment.LastApprovedTierFallback
            : QuotationSendPriceSegment.ApprovedTier, row.ComparisonBasis);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LastBoundaryPreservesInclusivityAndUsesLastTierForUncoveredPoint(bool lastInclusive)
    {
        var (line, standard) = Setup(10000, 10000, 29400);
        standard.PriceTiers.Add(Tier(1025, 3000, 30200));
        standard.PriceTiers.Add(Tier(6025, 10000, 29400, maxInclusive: lastInclusive));
        var row = Assert.Single(QuotationSendPriceComparison.Compare(line, standard));
        Assert.Equal(29400, row.ReferenceUnitPrice);
        Assert.Equal(lastInclusive ? QuotationSendPriceSegment.ApprovedTier
            : QuotationSendPriceSegment.LastApprovedTierFallback, row.ComparisonBasis);
        Assert.False(row.IsBelowReference);
    }

    [Theory]
    [InlineData(3000, 4000, 29400)]
    [InlineData(3500, 4000, 29400)]
    [InlineData(3000, 3025, 29800)]
    [InlineData(6001, 6024, 29400)]
    [InlineData(10000, null, 29000)]
    [InlineData(5000, 10000, 29000)]
    [InlineData(10025, 20000, 29000)]
    [InlineData(20000, null, 29000)]
    public void ScreenshotRangesWithHighCustomerPriceNeverRequireBoard(decimal min, int? max, decimal highestReference)
    {
        var (line, standard) = Setup(min, max, 500000);
        standard.PriceTiers.Add(Tier(1025, 3000, 29800));
        standard.PriceTiers.Add(Tier(3025, 6000, 29400));
        standard.PriceTiers.Add(Tier(6025, 10000, 29000));
        var comparisons = QuotationSendPriceComparison.Compare(line, standard);
        Assert.NotEmpty(comparisons);
        Assert.All(comparisons, x => Assert.True(x.DifferenceAmount > 0));
        Assert.Equal(highestReference, comparisons.Max(x => x.StandardUnitPrice));
        Assert.All(comparisons, x =>
        {
            Assert.Equal(highestReference, x.ReferenceUnitPrice);
            Assert.False(x.IsBelowReference);
        });
    }

    [Theory]
    [InlineData(true, 29400)]
    [InlineData(false, 29400)]
    public void EqualityPointRespectsCustomerInclusivity(bool inclusive, decimal reference)
    {
        var (line, standard) = Setup(3000, 4000, 29400);
        line.PriceTiers.Single().MinInclusive = inclusive;
        // No gap: excluding exactly 3000 must exclude the more expensive lower tier.
        standard.PriceTiers.Add(Tier(1025, 3000, 29800));
        standard.PriceTiers.Add(Tier(3000, 6000, 29400, minInclusive: false));
        Assert.All(QuotationSendPriceComparison.Compare(line, standard), x => Assert.Equal(reference, x.ReferenceUnitPrice));
    }

    [Fact]
    public void DisabledTiersAndCommonSellingPriceDoNotInfluenceReference()
    {
        var (line, standard) = Setup(350, 600, 90, 10);
        standard.StandardSellingPrice = 999999;
        standard.PriceTiers.Add(Tier(300, 600, 100));
        var disabled = Tier(300, 600, 999999);
        disabled.IsActive = false;
        standard.PriceTiers.Add(disabled);
        line.PriceTiers.Add(new QuotationLinePriceTier { IsActive = false });
        Assert.False(Assert.Single(QuotationSendPriceComparison.Compare(line, standard)).IsBelowReference);
    }

    [Fact]
    public void MoneyUsesPersistedSixDecimalPrecisionAndSumOfPriceAndCommission()
    {
        var (line, standard) = Setup(350, 600, 90.0000005m, 9.9999995m);
        standard.PriceTiers.Add(Tier(300, 600, 100.0000014m));
        var row = Assert.Single(QuotationSendPriceComparison.Compare(line, standard));
        Assert.Equal(100.000001m, row.CustomerUnitPrice);
        Assert.Equal(0m, row.ReferenceDifferenceAmount);
    }

    [Fact]
    public void MissingOrInactiveTiersNeverFallBackToZero()
    {
        var (line, standard) = Setup(350, 600, 500000);
        Assert.Throws<InvalidOperationException>(() => QuotationSendPriceComparison.Compare(line, standard));
        standard.PriceTiers.Add(new ProductPricingTier { UnitPrice = 100, IsActive = false });
        Assert.Throws<InvalidOperationException>(() => QuotationSendPriceComparison.Compare(line, standard));
    }

    [Fact]
    public void ZeroStandardProducesNullPercentageAndNoFalseBelowStandard()
    {
        var (line, standard) = Setup(350, 600, 0);
        standard.PriceTiers.Add(Tier(300, 600, 0));
        Assert.Null(Assert.Single(QuotationSendPriceComparison.Compare(line, standard)).DifferencePercent);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void ValidityEndsExactlyAtConfiguredExpiry(int offsetSeconds, bool effective)
    {
        var approvedAt = new DateTime(2026, 10, 1);
        var version = new ProductPricingVersion { ApprovedAt = approvedAt };
        Assert.Equal(effective, QuotationSendService.IsEffective(version, approvedAt.AddDays(30).AddSeconds(offsetSeconds), 30));
        Assert.False(QuotationSendService.IsEffective(version, approvedAt.AddSeconds(-1), 30));
        Assert.True(QuotationSendService.IsEffective(version, approvedAt.AddYears(1), 0));
    }

    private static (QuotationLine, ProductPricingVersion) Setup(decimal min, decimal? max, decimal price, decimal commission = 0)
    {
        var tier = new QuotationLinePriceTier { MinQuantity = min, MaxQuantity = max };
        tier.SetPrices(price, commission);
        return (new QuotationLine { ProductExternalIdSnapshot = "P1", PriceTiers = [tier] }, new ProductPricingVersion());
    }

    private static ProductPricingTier Tier(decimal min, decimal? max, decimal price, bool minInclusive = true, bool maxInclusive = true)
        => new() { MinQuantity = min, MaxQuantity = max, UnitPrice = price, MinInclusive = minInclusive, MaxInclusive = maxInclusive };
}
