using HRM.Domain.Enums.Notifications;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

[Flags]
internal enum SampleRequestNotificationAudience
{
    None = 0,
    LabLeaders = 1,
    LabAdmins = 2,
    CustomerSales = 4,
    Presidents = 8
}

internal static class SampleRequestNotificationAudienceRules
{
    internal static SampleRequestNotificationAudience For(TopicNotifications topic, string? workflowEventType = null)
        => topic == TopicNotifications.SampleRequestMessageCreated && workflowEventType == SampleRequestLabPauseRules.Rejected
            ? SampleRequestNotificationAudience.CustomerSales | SampleRequestNotificationAudience.LabLeaders | SampleRequestNotificationAudience.LabAdmins
            : topic == TopicNotifications.SampleRequestMessageCreated && workflowEventType is SampleRequestLabPauseRules.Resumed or SampleFlowRules.LabFinalizationRequested or SampleFlowRules.ChipFinalizationRequested or
            SampleFlowRules.ChipThenSampleRequested or SampleFlowRules.ChipRequested or SampleFlowRules.KgRequested or SampleFlowRules.CompletionDeferred
            ? SampleRequestNotificationAudience.LabLeaders | SampleRequestNotificationAudience.LabAdmins
            : topic switch
    {
        TopicNotifications.SampleRequestMessageCreated => SampleRequestNotificationAudience.LabLeaders,
        TopicNotifications.SampleRequestUpdated or
        TopicNotifications.SampleRequestSampleSent or
        TopicNotifications.SampleRequestFormulaUpdateRequested or
        TopicNotifications.SampleRequestReferencePriceAvailable => SampleRequestNotificationAudience.CustomerSales,
        TopicNotifications.SampleRequestPriceQuoteRequested =>
            SampleRequestNotificationAudience.CustomerSales | SampleRequestNotificationAudience.Presidents,
        TopicNotifications.SampleRequestFormulaUpdateApproved or
        TopicNotifications.SampleRequestFormulaUpdateRejected or
        TopicNotifications.SampleRequestFormulaUpdateCancelled or
        TopicNotifications.SampleRequestCancelled or
        TopicNotifications.SampleRequestCustomerFeedbackRecorded =>
            SampleRequestNotificationAudience.LabLeaders | SampleRequestNotificationAudience.LabAdmins,
        _ => SampleRequestNotificationAudience.None
    };
}
