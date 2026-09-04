using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationManualPriceTierRulesTests
{
    [Fact]
    public void Validate_AllowsZeroPricesWhenRangesMatchPublishedPolicy()
    {
        var result = QuotationManualPriceTierRules.Validate(
            Requests(secondMin: 50m),
            Policy());

        Assert.Null(result);
    }

    [Fact]
    public void Validate_RejectsRangeThatDoesNotMatchPublishedPolicy()
    {
        var result = QuotationManualPriceTierRules.Validate(
            Requests(secondMin: 51m),
            Policy());

        Assert.NotNull(result);
    }

    private static IReadOnlyList<QuotationLinePriceTierRequest> Requests(decimal secondMin)
        =>
        [
            new QuotationLinePriceTierRequest
            {
                QuantityRangeLabel = "< 50",
                MaxQuantity = 50m,
                MinInclusive = true,
                MaxInclusive = false,
                UnitPrice = 0m,
                SortOrder = 0
            },
            new QuotationLinePriceTierRequest
            {
                QuantityRangeLabel = "50 - 100",
                MinQuantity = secondMin,
                MaxQuantity = 100m,
                MinInclusive = true,
                MaxInclusive = true,
                UnitPrice = 120_000m,
                SortOrder = 1
            }
        ];

    private static FormulaPricingPolicyDefinition Policy()
        => new(
            FormulaPricingProfile.Compound,
            10_000m,
            0m,
            FormulaPricingRoundingRule.Nearest,
            1m,
            [
                new FormulaPricingPolicyTierDefinition(
                    "< 50", null, 50m, true, false, 50_000m, 0),
                new FormulaPricingPolicyTierDefinition(
                    "50 - 100", 50m, 100m, true, true, 20_000m, 1)
            ]);
}
