using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.Pricing.Authorization;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>Price feedback is free text and may contain quoted amounts; use the selling-price capability.</summary>
internal static class SampleRequestPriceFeedbackVisibility
{
    internal static bool CanViewContent(PricingAccessDecision access) => access.CanViewApprovedSellingPrice;
    internal static readonly string[] PriceEventTypes =
    [
        SampleRequestWorkflowEvents.PriceAccepted, SampleRequestWorkflowEvents.PriceRevoked,
        SampleFlowRules.PriceRejected, SampleFlowRules.PriceNoResponse,
        "CustomerPriceConfirmed", "CustomerPriceConfirmationRevoked"
    ];
    internal static bool IsPriceEvent(string eventType) => PriceEventTypes.Contains(eventType);
    // Publication privacy is broader than customer outcome history. A BGD approval is not customer acceptance.
    internal static bool IsPrivatePublication(string eventType)
        => IsPriceEvent(eventType) || eventType == SampleFlowRules.StandardPriceApproved;
    internal static bool IsPriceAuditField(string? reason, string fieldName)
        => fieldName.Equals("CustomerPriceReference", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Equals("CustomerPriceConfirmationNote", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Equals("CustomerPriceQuotationLineId", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Equals("CustomerPriceConfirmation", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Equals("PriceReference", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Equals("QuotationLineId", StringComparison.OrdinalIgnoreCase) ||
            (IsPriceEvent(reason ?? "") && (fieldName.Equals("Note", StringComparison.OrdinalIgnoreCase) ||
                fieldName.Equals("Reference", StringComparison.OrdinalIgnoreCase) ||
                fieldName.Equals("RevocationReason", StringComparison.OrdinalIgnoreCase)));


    internal static void Apply(SampleRequestSampleTrialReportDto item, PricingAccessDecision access)
    {
        // Compact/general feedback never falls back to private price text, even for price viewers.
        item.FeedbackSummaries = item.FeedbackSummaries.Select(x => x.Kind == "Price"
            ? x with { Note = x.IsSharedNote ? x.Note : null, IsNoteHidden = !x.IsSharedNote } : x).ToArray();
        if (CanViewContent(access)) return;
        item.IsCustomerPriceContentHidden = true;
        item.CustomerPriceReference = null;
        item.CustomerPriceConfirmationNote = null;
        item.CustomerPriceQuotationLineId = null;
        if (item.CustomerPriceConfirmation is { } confirmation)
            item.CustomerPriceConfirmation = confirmation with
            { Note = null, PriceReference = null, QuotationLineId = null, IsContentHidden = true };

    }
}
