using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationPriceTierBuilderTests
{
    [Fact]
    public void Build_AllowsEmptyTierSnapshot_WhenDraftLineMayHaveMissingPrice()
    {
        var result = QuotationPriceTierBuilder.Build(
            Guid.NewGuid(),
            QuotationLinePriceMode.Tiered,
            quantity: 1m,
            fixedUnitPrice: 0m,
            requests: [],
            fieldPath: "lines[0]",
            allowMissingPrice: true);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0m, result.Data.EffectiveUnitPrice);
        Assert.Empty(result.Data.PriceTiers);
    }

    [Fact]
    public void Build_RejectsEmptyTierSnapshot_WhenCompletePriceIsRequired()
    {
        var result = QuotationPriceTierBuilder.Build(
            Guid.NewGuid(),
            QuotationLinePriceMode.Tiered,
            quantity: 1m,
            fixedUnitPrice: 0m,
            requests: [],
            fieldPath: "lines[0]");

        Assert.False(result.Success);
        Assert.Contains("at least one tier", result.Message);
    }

    [Fact]
    public void Build_StillRejectsPartiallyEnteredTierWithoutPositivePrice()
    {
        var result = QuotationPriceTierBuilder.Build(
            Guid.NewGuid(),
            QuotationLinePriceMode.Tiered,
            quantity: 1m,
            fixedUnitPrice: 0m,
            requests:
            [
                new QuotationLinePriceTierRequest
                {
                    QuantityRangeLabel = "< 50 kg",
                    MaxQuantity = 50m,
                    MaxInclusive = false,
                    UnitPrice = 0m,
                    SortOrder = 0
                }
            ],
            fieldPath: "lines[0]",
            allowMissingPrice: true);

        Assert.False(result.Success);
        Assert.Contains("positive unitPrice", result.Message);
    }
}
