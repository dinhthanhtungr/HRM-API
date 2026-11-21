using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

internal static class SampleTrialFeedbackRules
{
    public static string? Validate(string requestStatus, SampleTrialStatus trialStatus, SampleTrialStatus targetStatus)
    {
        if (trialStatus is not SampleTrialStatus.SampleSent and not SampleTrialStatus.WaitingCustomerFeedback and not SampleTrialStatus.PriceQuote)
            return "Customer feedback can only be recorded for a sent sample awaiting customer feedback.";

        if (requestStatus is "Cancelled" or "LabDeclined")
            return "A cancelled sample request cannot receive customer feedback.";

        if (requestStatus == SampleRequestStatus.Completed.ToString() &&
            targetStatus is SampleTrialStatus.Approved or SampleTrialStatus.Cancelled)
            return "A completed sample request cannot be completed again or cancelled through another trial.";

        return null;
    }

    public static bool CanReopen(string requestStatus)
        => requestStatus != SampleRequestStatus.Completed.ToString() && requestStatus != SampleRequestStatus.Cancelled.ToString() && requestStatus != "LabDeclined";
}
