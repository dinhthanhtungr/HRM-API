using System.Text.Json;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.Notifications;

public sealed class NotificationTopicCatalogTests
{
    [Fact]
    public void EveryDeclaredTopic_HasConfiguredPresentation()
    {
        var unconfiguredTopics = Enum.GetValues<TopicNotifications>()
            .Where(topic => !NotificationTopicCatalog.IsConfigured(topic))
            .ToArray();

        Assert.Empty(unconfiguredTopics);
    }

    [Theory]
    [InlineData(TopicNotifications.MerchandiseOrderDeliveryPaused, "sales_order", "delivery")]
    [InlineData(TopicNotifications.ComplaintInitialDecision, "sales_order", "complaint")]
    [InlineData(TopicNotifications.MfgProductionOrderUpdated, "production", "change")]
    [InlineData(TopicNotifications.QuotationPricingApproved, "quotation", "pricing")]
    public void Definition_SeparatesBusinessCategoryFromEventGroup(
        TopicNotifications topic,
        string expectedCategory,
        string expectedEventGroup)
    {
        var definition = NotificationTopicCatalog.GetDefinition(topic);

        Assert.Equal(expectedCategory, definition.CategoryCode);
        Assert.Equal(expectedEventGroup, definition.EventGroupCode);
    }

    [Fact]
    public void PayloadComposer_ProducesTypedContextAndThreadReferences()
    {
        var aggregateId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var request = new PublishNotificationRequest
        {
            Topic = TopicNotifications.QuotationRequested,
            AggregateId = aggregateId,
            AggregateCode = "BBG260700001",
            ConversationId = conversationId,
            MessageId = messageId,
            PayloadJson = "{\"isUrgent\":true}"
        };

        var payloadJson = NotificationPayloadComposer.Compose(request);
        var notification = new NotificationDto
        {
            Topic = request.Topic,
            CreatedDate = NotificationTopicCategoryRules.CurrentDataStartDate,
            PayloadJson = payloadJson
        };

        NotificationPayloadPresentation.Apply(notification);

        Assert.NotNull(payloadJson);
        using var payload = JsonDocument.Parse(payloadJson!);
        Assert.True(payload.RootElement.GetProperty("isUrgent").GetBoolean());
        Assert.Equal("Quotation", notification.Context?.AggregateType);
        Assert.Equal(aggregateId, notification.Context?.AggregateId);
        Assert.Equal("BBG260700001", notification.Context?.AggregateCode);
        Assert.Equal(conversationId, notification.ConversationId);
        Assert.Equal(messageId, notification.MessageId);
    }

    [Fact]
    public void HistoricalNotification_IsAlwaysLegacyData()
    {
        var notification = new NotificationDto
        {
            Topic = TopicNotifications.MerchandiseOrderDeliveryPaused,
            CreatedDate = NotificationTopicCategoryRules.CurrentDataStartDate.AddTicks(-1)
        };

        Assert.Equal(NotificationCategoryCodes.LegacyData, notification.CategoryCode);
        Assert.Equal(NotificationCategoryCodes.LegacyData, notification.EventGroupCode);
    }
}
