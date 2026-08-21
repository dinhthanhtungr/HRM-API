using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class FormulaPricingPolicyRulesTests
{
    [Fact]
    public void BuildTiers_RejectsOverlappingInclusiveRanges()
    {
        var result = FormulaPricingPolicyRules.BuildTiers(
            Guid.NewGuid(),
            [
                new FormulaPricingPolicyTierRequest
                {
                    QuantityRangeLabel = "0-100", MinQuantity = 0m, MaxQuantity = 100m,
                    MinInclusive = true, MaxInclusive = true, PriceOffset = 0m, SortOrder = 0
                },
                new FormulaPricingPolicyTierRequest
                {
                    QuantityRangeLabel = "100+", MinQuantity = 100m,
                    MinInclusive = true, MaxInclusive = true, PriceOffset = null, SortOrder = 1
                }
            ]);

        Assert.False(result.Success);
        Assert.Contains("cannot overlap", result.Message);
    }

    [Theory]
    [InlineData("VND", true)]
    [InlineData("usd", true)]
    [InlineData("VN", false)]
    [InlineData("VND1", false)]
    public void IsValidCurrency_AcceptsOnlyThreeLetterCurrencyCodes(string currency, bool expected)
        => Assert.Equal(expected, FormulaPricingPolicyRules.IsValidCurrency(currency));

    [Fact]
    public void PublishValidation_RequiresDraftVersionEffectiveDateAndTiers()
    {
        Assert.Null(FormulaPricingPolicyRules.ValidatePublish(
            FormulaPricingPolicyStatus.Draft, 2, DateTime.UtcNow, 1));
        Assert.NotNull(FormulaPricingPolicyRules.ValidatePublish(
            FormulaPricingPolicyStatus.Published, 2, DateTime.UtcNow, 1));
        Assert.NotNull(FormulaPricingPolicyRules.ValidatePublish(
            FormulaPricingPolicyStatus.Draft, 0, DateTime.UtcNow, 1));
        Assert.Equal(3, FormulaPricingPolicyRules.GetNextVersion(2));
    }

    [Fact]
    public void MissingPolicy_UsesStableErrorCode()
        => Assert.Equal("PricingPolicyMissing", FormulaPricingPolicyRules.PricingPolicyMissing);
}
