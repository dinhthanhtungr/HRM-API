using System.Text.Json.Nodes;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.Notifications.Dtos;

internal static class NotificationPayloadComposer
{
    public static string? Compose(PublishNotificationRequest request)
    {
        var hasContext = request.AggregateId.HasValue || !string.IsNullOrWhiteSpace(request.AggregateCode);
        var hasThreadReference = request.ConversationId.HasValue || request.MessageId.HasValue;
        if (!hasContext && !hasThreadReference)
        {
            return request.PayloadJson;
        }

        var root = string.IsNullOrWhiteSpace(request.PayloadJson)
            ? new JsonObject()
            : JsonNode.Parse(request.PayloadJson) as JsonObject
                ?? throw new ArgumentException("Notification PayloadJson must be a JSON object.", nameof(request));

        if (hasContext)
        {
            var definition = NotificationTopicCatalog.GetDefinition(request.Topic);
            var context = root["context"] as JsonObject ?? new JsonObject();

            if (!string.IsNullOrWhiteSpace(definition.AggregateType))
            {
                context["aggregateType"] = definition.AggregateType;
            }

            if (request.AggregateId.HasValue)
            {
                context["aggregateId"] = request.AggregateId.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.AggregateCode))
            {
                context["aggregateCode"] = request.AggregateCode.Trim();
            }

            root["context"] = context;
        }

        if (request.ConversationId.HasValue)
        {
            root["conversationId"] = request.ConversationId.Value;
        }

        if (request.MessageId.HasValue)
        {
            root["messageId"] = request.MessageId.Value;
        }

        return root.ToJsonString();
    }
}
