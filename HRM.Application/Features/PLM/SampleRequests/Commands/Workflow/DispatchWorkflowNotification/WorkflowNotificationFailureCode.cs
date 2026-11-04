namespace HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;

/// <summary>Non-sensitive operational reasons; never include customer content or exception details.</summary>
public enum WorkflowNotificationFailureCode
{
    Unknown,
    JobUnavailable,
    InvalidPayload,
    EventActorUnavailable,
    FormulaUnavailable,
    MissingPresident,
    CommunicationScopeDenied,
    InvalidRecipients,
    PricingRequestUnavailable,
    MessageCreationFailed,
    HistoricalPricePublicationRequiresReview
}

internal static class WorkflowNotificationFailureRules
{
    public static WorkflowNotificationFailureCode FromMessage(string? message) => message switch
    {
        "No active President employee was found in the current company." => WorkflowNotificationFailureCode.MissingPresident,
        "This product is outside your communication scope." => WorkflowNotificationFailureCode.CommunicationScopeDenied,
        "Some recipients do not exist or are inactive." => WorkflowNotificationFailureCode.InvalidRecipients,
        "Sample request was not found or does not belong to the formula product." => WorkflowNotificationFailureCode.PricingRequestUnavailable,
        _ => WorkflowNotificationFailureCode.MessageCreationFailed
    };
}
