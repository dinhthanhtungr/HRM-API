using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationTierPriceMatcherTests
{
    [Fact]
    public void Find_PrefersExactQuantityRange_WhenLabelChanged()
    {
        var expectedDate = new DateTime(2026, 8, 20, 9, 30, 0);
        var references = new[]
        {
            new QuotationTierPriceReference(
                "50 - 99 kg",
                50m,
                99m,
                true,
                true,
                125_000m,
                3,
                expectedDate)
        };
        var target = new QuotationLinePriceTierDto
        {
            QuantityRangeLabel = "50-99 kg",
            MinQuantity = 50m,
            MaxQuantity = 99m,
            MinInclusive = true,
            MaxInclusive = true,
            SortOrder = 1
        };

        var result = QuotationTierPriceMatcher.Find(references, target);

        Assert.NotNull(result);
        Assert.Equal(125_000m, result.UnitPrice);
        Assert.Equal(expectedDate, result.PriceDate);
    }

    [Fact]
    public void Find_FallsBackToLabelThenSortOrder_ForLegacySnapshots()
    {
        var references = new[]
        {
            new QuotationTierPriceReference(
                "Legacy label",
                null,
                null,
                true,
                true,
                90_000m,
                2,
                null),
            new QuotationTierPriceReference(
                "100 - 300 KG",
                null,
                null,
                false,
                false,
                95_000m,
                9,
                null)
        };

        var labelMatch = QuotationTierPriceMatcher.Find(
            references,
            new QuotationLinePriceTierDto
            {
                QuantityRangeLabel = "100 - 300 kg",
                MinQuantity = 100m,
                MaxQuantity = 300m,
                SortOrder = 1
            });
        var sortOrderMatch = QuotationTierPriceMatcher.Find(
            references,
            new QuotationLinePriceTierDto
            {
                QuantityRangeLabel = "Unknown",
                MinQuantity = 325m,
                MaxQuantity = 600m,
                SortOrder = 2
            });

        Assert.Equal(95_000m, labelMatch?.UnitPrice);
        Assert.Equal(90_000m, sortOrderMatch?.UnitPrice);
    }
}
