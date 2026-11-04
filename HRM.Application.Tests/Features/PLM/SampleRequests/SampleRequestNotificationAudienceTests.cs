using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestNotificationAudienceTests
{
    [Fact]
    public void HttpClientCannotSelectInitialMessageDeliveryPolicy()
    {
        var command = System.Text.Json.JsonSerializer.Deserialize<
            HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage.SendSampleRequestMessageCommand>(
            "{\"isInitialMessage\":true,\"useDefaultSilentWatchersWhenOmitted\":true}",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.False(command.IsInitialMessage);
        Assert.False(command.UseDefaultSilentWatchersWhenOmitted);
    }

    [Theory]
    [InlineData(TopicNotifications.SampleRequestDirectPatchNotified)]
    [InlineData(TopicNotifications.SampleRequestFormulaCompleted)]
    [InlineData(TopicNotifications.SampleRequestDataChangeRequested)]
    [InlineData(TopicNotifications.SampleRequestDataChangeApproved)]
    [InlineData(TopicNotifications.SampleRequestDataChangeRejected)]
    public void InformationalEventsDoNotBroadcast(TopicNotifications topic)
        => Assert.Equal(SampleRequestNotificationAudience.None, SampleRequestNotificationAudienceRules.For(topic));

    [Theory]
    [InlineData(TopicNotifications.SampleRequestUpdated)]
    [InlineData(TopicNotifications.SampleRequestSampleSent)]
    [InlineData(TopicNotifications.SampleRequestFormulaUpdateRequested)]
    [InlineData(TopicNotifications.SampleRequestReferencePriceAvailable)]
    public void SalesActionsNotifyCustomerSales(TopicNotifications topic)
        => Assert.Equal(SampleRequestNotificationAudience.CustomerSales, SampleRequestNotificationAudienceRules.For(topic));

    [Theory]
    [InlineData(TopicNotifications.SampleRequestFormulaUpdateApproved)]
    [InlineData(TopicNotifications.SampleRequestFormulaUpdateRejected)]
    [InlineData(TopicNotifications.SampleRequestFormulaUpdateCancelled)]
    [InlineData(TopicNotifications.SampleRequestCancelled)]
    [InlineData(TopicNotifications.SampleRequestCustomerFeedbackRecorded)]
    public void LabActionsNotifyLabLeadersAndAdmins(TopicNotifications topic)
        => Assert.Equal(SampleRequestNotificationAudience.LabLeaders | SampleRequestNotificationAudience.LabAdmins,
            SampleRequestNotificationAudienceRules.For(topic));

    [Fact]
    public void PriceReviewAlsoNotifiesPresident()
        => Assert.Equal(SampleRequestNotificationAudience.CustomerSales | SampleRequestNotificationAudience.Presidents,
            SampleRequestNotificationAudienceRules.For(TopicNotifications.SampleRequestPriceQuoteRequested));

    [Fact]
    public void OrdinaryMessagesNotifyOnlyLabLeadersByDefault()
        => Assert.Equal(SampleRequestNotificationAudience.LabLeaders,
            SampleRequestNotificationAudienceRules.For(TopicNotifications.SampleRequestMessageCreated));
}
