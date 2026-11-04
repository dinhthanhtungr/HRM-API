using HRM.Domain.Enums.Notifications;

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
    internal static SampleRequestNotificationAudience For(TopicNotifications topic) => topic switch
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
