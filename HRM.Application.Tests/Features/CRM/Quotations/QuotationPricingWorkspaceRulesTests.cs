using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationPricingWorkspaceRulesTests
{
    [Theory]
    [InlineData(true, 1, true, true, true, QuotationPricingWorkspaceState.Applied)]
    [InlineData(false, 0, true, true, true, QuotationPricingWorkspaceState.ApprovedAvailable)]
    [InlineData(false, 0, false, true, true, QuotationPricingWorkspaceState.Draft)]
    [InlineData(false, 0, false, false, true, QuotationPricingWorkspaceState.WaitingForPricing)]
    [InlineData(false, 0, false, false, false, QuotationPricingWorkspaceState.NoEligibleSource)]
    public void ResolveState_ReturnsHighestPriorityAvailableState(
        bool hasAppliedPricingVersion,
        int appliedTierCount,
        bool hasApprovedPricing,
        bool hasDraftPricing,
        bool hasEligibleSource,
        QuotationPricingWorkspaceState expected)
    {
        var result = QuotationPricingWorkspaceRules.ResolveState(
            hasAppliedPricingVersion ? Guid.NewGuid() : null,
            appliedTierCount,
            hasApprovedPricing,
            hasDraftPricing,
            hasEligibleSource);

        Assert.Equal(expected, result);
    }
}
