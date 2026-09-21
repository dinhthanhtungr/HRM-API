using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class SaleOrderApprovalPriceWarningRulesTests
{
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
