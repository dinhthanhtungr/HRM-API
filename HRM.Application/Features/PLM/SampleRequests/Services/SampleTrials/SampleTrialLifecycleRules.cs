using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

internal static class SampleTrialLifecycleRules
{
    public static bool IsClosedRequest(string status)
        => status is "Completed" or "Cancelled";

    public static string? ValidateCreate(string requestStatus, SampleTrialStatus status)
    {
        if (IsClosedRequest(requestStatus))
            return "A closed sample request cannot receive another trial.";
        return status is SampleTrialStatus.Approved or SampleTrialStatus.Failed or SampleTrialStatus.Cancelled or SampleTrialStatus.ReworkRequested
            ? "Create a trial before recording its result through the customer feedback action."
            : null;
    }

    public static string? ValidatePatch(string requestStatus, SampleTrialStatus currentStatus,
        SampleTrialStatus? targetStatus, bool changesDelivery, bool changesReplyStatus, bool changesReplyNote)
    {
        var changesStatus = targetStatus.HasValue && targetStatus != currentStatus;
        var terminalTrial = currentStatus is SampleTrialStatus.Approved or SampleTrialStatus.Failed or
            SampleTrialStatus.Cancelled or SampleTrialStatus.ReworkRequested;
        if ((IsClosedRequest(requestStatus) || terminalTrial) &&
            (changesStatus || changesDelivery || changesReplyStatus || changesReplyNote))
            return "Delivery and feedback of a closed request or completed trial cannot be changed through trial editing.";

        if (changesReplyStatus || (changesStatus && targetStatus is SampleTrialStatus.Approved or
            SampleTrialStatus.Failed or SampleTrialStatus.Cancelled or SampleTrialStatus.ReworkRequested))
            return "Use the customer feedback action to record the result of this trial.";

        return null;
    }
}
