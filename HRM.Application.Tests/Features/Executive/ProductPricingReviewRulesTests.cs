using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.Executive;

public sealed class ProductPricingReviewRulesTests
{
    [Fact]
    public void SelectLatestCreatedFormulaUse_DoesNotPreferVuOrVa()
    {
        var olderVu = FormulaUse(PricingReviewSourceType.VU, new DateTime(2026, 9, 10));
        var newerVa = FormulaUse(PricingReviewSourceType.VA, new DateTime(2026, 9, 11));

        var vaResult = ProductPricingReviewRules.SelectLatestCreatedFormulaUse(olderVu, newerVa);
        var vuResult = ProductPricingReviewRules.SelectLatestCreatedFormulaUse(
            FormulaUse(PricingReviewSourceType.VU, new DateTime(2026, 9, 12)),
            newerVa);

        Assert.Equal(PricingReviewSourceType.VA, vaResult!.SourceType);
        Assert.Equal(PricingReviewSourceType.VU, vuResult!.SourceType);
    }

    private static PricingReviewCurrentFormulaUseDto FormulaUse(
        PricingReviewSourceType sourceType,
        DateTime createdAt)
        => new()
        {
            SourceType = sourceType,
            SourceId = Guid.NewGuid(),
            CreatedAt = createdAt
        };

    [Fact]
    public void PublicProfitMarginPercent_IsCalculatedOnSellingPrice()
    {
        var margin = PricingMarginCalculator.CalculateProfitMarginPercent(53_000m, 33_750m);

        Assert.Equal(36.3208m, margin);
    }

    [Theory]
    [InlineData("President", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", false)]
    public void Access_IsLimitedToPricingManagers(string role, bool allowed)
    {
        var result = ProductPricingReviewRules.GetContext(new User(role));

        Assert.Equal(allowed, result.Success);
    }

    [Fact]
    public void SourceTypes_MapToCanonicalLegacyTypes()
    {
        Assert.Equal(
            ProductPricingSourceType.Formula,
            ProductPricingReviewRules.ToLegacy(PricingReviewSourceType.VU));
        Assert.Equal(
            ProductPricingSourceType.ManufacturingFormula,
            ProductPricingReviewRules.ToLegacy(PricingReviewSourceType.VA));
    }

    [Theory]
    [InlineData("Approved", true)]
    [InlineData("Completed", true)]
    [InlineData("SampleSent", true)]
    [InlineData("PendingSaleConfirmation", true)]
    [InlineData("Draft", false)]
    [InlineData("Cancelled", false)]
    [InlineData("Rejected", false)]
    [InlineData(null, false)]
    public void VuEligibility_OnlyAllowsPricingReviewWorkflowStatuses(string? status, bool expected)
    {
        Assert.Equal(expected, ProductPricingReviewRules.IsEligibleVuFormulaStatus(status));
    }

    private sealed class User(string role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.NewGuid();
        public Guid? EmployeeId => Guid.NewGuid();
        public Guid? CompanyId => Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string candidate) =>
            string.Equals(candidate, role, StringComparison.Ordinal);
    }
}
