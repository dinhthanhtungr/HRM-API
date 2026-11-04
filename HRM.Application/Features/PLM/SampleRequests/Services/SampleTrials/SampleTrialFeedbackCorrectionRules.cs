namespace HRM.Application.Features.PLM.SampleRequests.SampleTrials;

internal static class SampleTrialFeedbackCorrectionRules
{
    public const string AuditReason = "SampleTrialFeedbackCorrected";
    public const string AuditTable = "SampleRequestSampleTrials";

    public static bool CanCorrect(bool hasPermission, Guid? employee, Guid owner,
        int trialNo, int latestTrialNo, bool hasRecordedFeedback, bool canActForOwner = false)
        => hasPermission && employee.HasValue && employee != Guid.Empty && (employee == owner || canActForOwner) &&
           trialNo == latestTrialNo && hasRecordedFeedback;

    public static string? Validate(DateTime date, DateTime? sentDate, string? note)
    {
        if (date == default) return "Customer feedback date is required.";
        if (sentDate.HasValue && date.Date < sentDate.Value.Date)
            return "Customer feedback date cannot precede sample delivery.";
        return note?.Length > 5000 ? "Customer feedback note cannot exceed 5000 characters." : null;
    }
}
