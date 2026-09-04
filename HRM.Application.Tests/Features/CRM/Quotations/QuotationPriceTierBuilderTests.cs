using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationPriceTierBuilderTests
{
    [Fact]
    public void Build_RejectsEmptyTierSnapshot()
    {
        var result = QuotationPriceTierBuilder.Build(
            Guid.NewGuid(),
            quantity: 1m,
            requests: [],
            fieldPath: "lines[0]");

        Assert.False(result.Success);
        Assert.Contains("at least one active tier", result.Message);
    }

    [Fact]
    public void Build_RejectsTierSnapshotWithoutAnActiveTier()
    {
        var result = QuotationPriceTierBuilder.Build(
            Guid.NewGuid(),
            quantity: 1m,
            requests:
            [
                new QuotationLinePriceTierRequest
                {
                    QuantityRangeLabel = ">= 50 kg",
                    MinQuantity = 50m,
                    MinInclusive = true,
                    UnitPrice = 10m,
                    SortOrder = 0,
                    IsActive = false
                }
            ],
            fieldPath: "lines[0]");

        Assert.False(result.Success);
        Assert.Contains("at least one active tier", result.Message);
    }

    [Fact]
    public void Build_AllowsActiveTierThatDoesNotMatchTheLineQuantity()
    {
        var result = QuotationPriceTierBuilder.Build(
            Guid.NewGuid(),
            quantity: 1m,
            requests:
            [
                new QuotationLinePriceTierRequest
                {
                    QuantityRangeLabel = ">= 50 kg",
                    MinQuantity = 50m,
                    MinInclusive = true,
                    UnitPrice = 10m,
                    SortOrder = 0,
                    IsActive = true
                }
            ],
            fieldPath: "lines[0]");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(10m, result.Data.EffectiveUnitPrice);
        Assert.Single(result.Data.PriceTiers);
    }

    [Fact]
    public void Build_AllowsZeroTierPrice()
    {
        var result = QuotationPriceTierBuilder.Build(
            Guid.NewGuid(),
            quantity: 1m,
            requests:
            [
                new QuotationLinePriceTierRequest
                {
                    QuantityRangeLabel = "< 50 kg",
                    MaxQuantity = 50m,
                    MaxInclusive = false,
                    UnitPrice = 0m,
                    SortOrder = 0,
                    IsActive = true
                }
            ],
            fieldPath: "lines[0]");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0m, result.Data.EffectiveUnitPrice);
    }

    [Fact]
    public void Build_RejectsNegativeTierPrice()
    {
        var result = QuotationPriceTierBuilder.Build(
            Guid.NewGuid(),
            quantity: 1m,
            requests:
            [
                new QuotationLinePriceTierRequest
                {
                    QuantityRangeLabel = "< 50 kg",
                    MaxQuantity = 50m,
                    MaxInclusive = false,
                    UnitPrice = -1m,
                    SortOrder = 0,
                    IsActive = true
                }
            ],
            fieldPath: "lines[0]");

        Assert.False(result.Success);
        Assert.Contains("non-negative unitPrice", result.Message);
    }
}
