using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>
/// Applies the central pricing decision to the pricing fields displayed on Sample Request trial cards.
/// </summary>
internal static class SampleRequestTrialPricingVisibility
{
    public static void Apply(
        SampleRequestSampleTrialReportDto target,
        decimal? approvedStandardSellingPrice,
        string? publisherNote,
        DateTime? approvedAt,
        decimal? systemCalculatedStandardSellingPrice,
        PricingAccessDecision access)
    {
        target.ApprovedStandardSellingPrice = access.CanViewApprovedSellingPrice
            ? approvedStandardSellingPrice
            : null;
        target.PublisherNote = access.CanViewApprovedSellingPrice ? publisherNote : null;
        target.StandardSellingPriceApprovedAt = access.CanViewApprovedSellingPrice
            ? approvedAt
            : null;
        target.SystemCalculatedStandardSellingPrice = access.CanViewSystemCalculatedPrice
            ? systemCalculatedStandardSellingPrice
            : null;
    }
}
