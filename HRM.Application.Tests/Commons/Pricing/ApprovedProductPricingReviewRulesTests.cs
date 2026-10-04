using HRM.Application.Commons.Pricing.Rules;

namespace HRM.Application.Tests.Commons.Pricing;

public sealed class ApprovedProductPricingReviewRulesTests
{
    [Fact]
    public void Current_approved_price_remains_usable_before_review_due_date()
    {
        var now = new DateTime(2026, 9, 20, 8, 0, 0);

        Assert.True(ApprovedProductPricingReviewRules.IsCurrent(
            now.AddDays(-29), now, reviewAfterDays: 30));
    }

    [Fact]
    public void Expired_approved_price_is_not_usable_on_review_due_date()
    {
        var now = new DateTime(2026, 9, 20, 8, 0, 0);

        Assert.False(ApprovedProductPricingReviewRules.IsCurrent(
            now.AddDays(-30), now, reviewAfterDays: 30));
    }

    [Fact]
    public void Disabled_review_window_keeps_approved_price_usable()
    {
        Assert.True(ApprovedProductPricingReviewRules.IsCurrent(
            new DateTime(2020, 1, 1), new DateTime(2026, 9, 20), reviewAfterDays: 0));
    }
}
