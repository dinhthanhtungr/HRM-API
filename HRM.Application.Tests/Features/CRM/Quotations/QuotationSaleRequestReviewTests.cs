using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationSaleRequestReviewTests
{
    [Theory]
    [InlineData(-1, ProductStandardPriceState.Active)]
    [InlineData(0, ProductStandardPriceState.Active)]
    [InlineData(1, ProductStandardPriceState.PendingReapproval)]
    public void SaleRequest_OnlyRequiresReapprovalWhenNewerThanApproval(
        int requestOffset, ProductStandardPriceState expected)
    {
        var approvedAt = new DateTime(2026, 10, 8, 9, 0, 0);
        var state = new ProductStandardPriceStateResult(
            ProductStandardPriceState.Active, false, false, null, approvedAt);

        var result = ProductStandardPriceStateResolver.ApplySaleRequest(
            state, approvedAt.AddMinutes(requestOffset));

        Assert.Equal(expected, result.State);
        Assert.False(result.HasFormulaConfirmationPending);
        Assert.False(result.IsPricingReviewExpired);
    }

    [Fact]
    public void WithoutActiveRequest_KeepsExistingReviewReasons()
    {
        var state = new ProductStandardPriceStateResult(
            ProductStandardPriceState.PendingReapproval, true, false, null, DateTime.UtcNow);

        Assert.Equal(state, ProductStandardPriceStateResolver.ApplySaleRequest(state, null));
    }

    [Fact]
    public void WithoutApprovedPrice_KeepsInitialApprovalState()
    {
        var state = new ProductStandardPriceStateResult(
            ProductStandardPriceState.PendingInitialApproval, false, false, null);

        Assert.Equal(state, ProductStandardPriceStateResolver.ApplySaleRequest(state, DateTime.UtcNow));
    }
}
