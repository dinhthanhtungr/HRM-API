using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationCommercialTermsTests
{
    [Fact]
    public void PriceTierBuilder_PersistsCommissionSeparatelyFromUnitPrice()
    {
        var result = QuotationPriceTierBuilder.Build(
            Guid.NewGuid(),
            quantity: 25m,
            requests:
            [
                new QuotationLinePriceTierRequest
                {
                    QuantityRangeLabel = "< 50 kg",
                    MaxQuantity = 50m,
                    MaxInclusive = false,
                    UnitPrice = 100_000m,
                    CommissionAmount = 5_000m,
                    SortOrder = 0
                }
            ],
            fieldPath: "lines[0]");

        Assert.True(result.Success);
        var tier = Assert.Single(result.Data!.PriceTiers);
        Assert.Equal(100_000m, result.Data.EffectiveUnitPrice);
        Assert.Equal(100_000m, tier.UnitPrice);
        Assert.Equal(5_000m, tier.CommissionAmount);
        Assert.Equal(105_000m, tier.CustomerUnitPrice);
        Assert.Equal(105_000m, tier.CustomerUnitPrice);
    }

    [Fact]
    public void PriceTierBuilder_RejectsNegativeCommission()
    {
        var result = QuotationPriceTierBuilder.Build(
            Guid.NewGuid(),
            quantity: 25m,
            requests:
            [
                new QuotationLinePriceTierRequest
                {
                    QuantityRangeLabel = "< 50 kg",
                    MaxQuantity = 50m,
                    MaxInclusive = false,
                    UnitPrice = 100_000m,
                    CommissionAmount = -1m,
                    SortOrder = 0
                }
            ],
            fieldPath: "lines[0]");

        Assert.False(result.Success);
        Assert.Contains("commissionAmount", result.Message);
    }

    [Fact]
    public void TermBuilder_NormalizesAndOrdersActiveTerms()
    {
        var quotationId = Guid.NewGuid();
        var result = QuotationTermBuilder.Build(
            quotationId,
            [
                new QuotationTermRequest
                {
                    LabelVi = " Thanh toán ",
                    LabelEn = " Payment term ",
                    ValueVi = " Thanh toán ngay ",
                    SortOrder = 1
                },
                new QuotationTermRequest
                {
                    LabelVi = "Giao hàng",
                    ValueVi = "5-7 ngày",
                    SortOrder = 0
                }
            ]);

        Assert.True(result.Success);
        var terms = Assert.IsAssignableFrom<IReadOnlyList<HRM.Domain.Entities.CustomerSchema.QuotationTerm>>(
            result.Data);
        Assert.Equal([0, 1], terms.Select(x => x.SortOrder));
        Assert.Equal("Giao hàng", terms[0].LabelVi);
        Assert.Equal("Thanh toán", terms[1].LabelVi);
        Assert.Equal(quotationId, terms[0].QuotationId);
    }
}
