using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class SaleOrderApprovalPriceWarningRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ForeignCurrency_RejectsMissingOrInvalidExchangeRate(decimal rate)
        => Assert.False(QuotationPricingCurrencyConverter.TryValidateQuotationCurrency("USD", rate, out _));
    [Theory]
    [InlineData("USD", 25000, 0.9, true)]
    [InlineData("USD", 30000, 0.9, false)]
    [InlineData("USD", 26200, 1, false)]
    [InlineData("VND", 1, 26200, false)]
    public void Comparison_UsesSameCurrencyAtOrderExchangeRate(string currency, decimal rate, decimal agreed, bool warning)
    {
        var standard = SaleOrderApprovalPriceWarningRules.ConvertApprovedPrice(26200m, currency, rate);
        Assert.Equal(warning, SaleOrderApprovalPriceWarningRules.IsBelowApprovedStandardPrice(agreed, standard));
    }
    [Theory]
    [InlineData(625, 96000)]
    [InlineData(1000, 96000)]
    [InlineData(1001, 98000)]
    [InlineData(1025, 95000)]
    [InlineData(3000, 95000)]
    [InlineData(3001, 98000)]
    public void ResolveApprovedPrice_UsesQuantityRangeOrBaseFallback(decimal quantity, decimal expected)
    {
        ProductPricingTier[] tiers = [
            new() { MinQuantity = 1025, MaxQuantity = 3000, UnitPrice = 95000, SortOrder = 6 },
            new() { MinQuantity = 625, MaxQuantity = 1000, UnitPrice = 96000, SortOrder = 5 }
        ];
        Assert.Equal(expected, SaleOrderApprovalPriceWarningRules.ResolveApprovedPrice(quantity, 98000, tiers));
    }

    [Fact]
    public void ResolveApprovedPrice_HonorsExclusiveBoundsActiveAndSortOrder()
    {
        ProductPricingTier[] tiers = [
            new() { UnitPrice = 1, SortOrder = 0, IsActive = false },
            new() { MinQuantity = 10, MaxQuantity = 20, MinInclusive = false, MaxInclusive = false, UnitPrice = 0, SortOrder = 1 },
            new() { UnitPrice = 90, SortOrder = 2 }
        ];
        Assert.Equal(90, SaleOrderApprovalPriceWarningRules.ResolveApprovedPrice(10, 100, tiers));
        Assert.Equal(0, SaleOrderApprovalPriceWarningRules.ResolveApprovedPrice(15, 100, tiers));
        Assert.Equal(90, SaleOrderApprovalPriceWarningRules.ResolveApprovedPrice(20, 100, tiers));
        Assert.Equal(100, SaleOrderApprovalPriceWarningRules.ResolveApprovedPrice(15, 100, []));
    }

    [Theory]
    [InlineData(95000, false)]
    [InlineData(94999, true)]
    [InlineData(96000, false)]
    public void TierPrice_PreventsFalseWarningAgainstBase(decimal agreed, bool warning)
    {
        var price = SaleOrderApprovalPriceWarningRules.ResolveApprovedPrice(1025, 98000,
            [new ProductPricingTier { MinQuantity = 1025, MaxQuantity = 3000, UnitPrice = 95000 }]);
        Assert.Equal(warning, SaleOrderApprovalPriceWarningRules.IsBelowApprovedStandardPrice(agreed, price));
    }

    [Fact]
    public void ShouldCheck_AllowsOnlyExternalMerchandiseOrders()
    {
        Assert.True(SaleOrderApprovalPriceWarningRules.ShouldCheck(OrderType.Merchandise, false));
        Assert.False(SaleOrderApprovalPriceWarningRules.ShouldCheck(OrderType.Internal, true));
        Assert.False(SaleOrderApprovalPriceWarningRules.ShouldCheck(OrderType.Complaint, false));
        Assert.False(SaleOrderApprovalPriceWarningRules.ShouldCheck(OrderType.SampleRequest, false));
        Assert.False(SaleOrderApprovalPriceWarningRules.ShouldCheck(OrderType.Merchandise, true));
    }

    [Theory]
    [InlineData(99, 100, true)]
    [InlineData(100, 100, false)]
    [InlineData(101, 100, false)]
    public void IsBelowApprovedStandardPrice_UsesStrictlyLowerComparison(
        decimal unitPriceAgreed,
        decimal approvedStandardSellingPrice,
        bool expected)
        => Assert.Equal(
            expected,
            SaleOrderApprovalPriceWarningRules.IsBelowApprovedStandardPrice(
                unitPriceAgreed,
                approvedStandardSellingPrice));
}
