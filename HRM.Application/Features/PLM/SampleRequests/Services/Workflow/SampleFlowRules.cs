using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

/// <summary>Lifecycle routing is owned by BE. Work-list inclusion is a separate rule.</summary>
internal static class SampleFlowRules
{
    public static readonly DateTime StartsAt = new(2026, 11, 1);
    public const string QuotationFinalized = "QuotationFormulaFinalized";
    public const string StandardPriceApproved = "StandardPriceApproved";
    public const string ChipFinalized = "ChipFormulaFinalized";
    public const string ChipReopened = "ChipFormulaReopened";
    public const string ChipFinalizationRequested = "ChipFormulaFinalizationRequested";
    public const string ChipFollowUpDeferred = "ChipCustomerFollowUpDeferred";
    public const string CompletionDeferred = "RequestCompletionDeferred";
    public const string KgRequested = "TrialSampleRequested";
    public const string ChipRequested = "ColorChipRequested";
    public const string ChipThenSampleRequested = "ColorChipThenSampleRequested";
    public const string QuotationRequired = "QuotationRequiredForCompletion";
    public const string LabFinalizationRequested = "LabFinalizationRequested";
    public const string CompletedWithoutTrial = "RequestCompletedWithoutTrial";
    public const string PriceFeedbackShared = "CustomerPriceFeedbackShared";
    public const string PriceRejected = "CustomerPriceRejected";
    public const string PriceNoResponse = "CustomerPriceNoResponse";
    public const string LowerPriceRequested = "LowerPriceFormulaRequested";
    public const string PriceFollowUpDeferred = "CustomerPriceFollowUpDeferred";

    public static bool IsLegacy(DateTime createdAt) => createdAt < StartsAt;
    public static bool IsOpen(string status) => status is not "Completed" and not "Cancelled" and not "LabDeclined";
    public static bool IsPending(SampleTrialStatus status) => status is SampleTrialStatus.SampleSent or
        SampleTrialStatus.WaitingCustomerFeedback or SampleTrialStatus.PriceQuote;

    public static bool IsModernChip(SampleRequest request, SampleRequestSampleTrial trial)
        => !IsLegacy(request.CreatedDate) && trial.DeliveryKind == SampleDeliveryKind.ColorChip;

    /// <summary>Accepted chips remain sent; rejection requests rework without completing or selecting a production formula.</summary>
    public static void ApplyChipReply(SampleRequest request, SampleRequestSampleTrial trial,
        SampleTrialStatus result, Guid actor, DateTime now)
    {
        trial.Status = result;
        if (!IsOpen(request.Status)) return;
        if (result is SampleTrialStatus.Approved or SampleTrialStatus.Failed)
        {
            if (trial.Formula is null) throw new InvalidOperationException("Chip formula is unavailable.");
            trial.Formula.Status = (result == SampleTrialStatus.Approved
                ? FormulaStatus.SampleSent : FormulaStatus.Rework).ToString();
            trial.Formula.UpdatedBy = actor;
            trial.Formula.UpdatedDate = now;
            request.Status = SampleRequestStatus.InProgress.ToString();
        }
        else if (result == SampleTrialStatus.Cancelled)
            request.Status = SampleRequestStatus.Cancelled.ToString();
        request.UpdatedBy = actor;
        request.UpdatedDate = now;
    }
}
