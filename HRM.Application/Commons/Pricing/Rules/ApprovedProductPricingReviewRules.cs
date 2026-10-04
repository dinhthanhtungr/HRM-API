namespace HRM.Application.Commons.Pricing.Rules;

/// <summary>
/// Determines whether an approved product price remains usable before its review due date.
/// </summary>
public static class ApprovedProductPricingReviewRules
{
    public static bool IsCurrent(DateTime approvedAt, DateTime now, int reviewAfterDays)
        => reviewAfterDays <= 0 || approvedAt.AddDays(reviewAfterDays) > now;
}
