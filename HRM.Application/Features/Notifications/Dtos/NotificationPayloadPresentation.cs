using System.Text.Json;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.Notifications.Dtos;

internal static class NotificationPayloadPresentation
{
    public static void Apply(NotificationDto notification)
    {
        if (string.IsNullOrWhiteSpace(notification.PayloadJson))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(notification.PayloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var root = document.RootElement;
            notification.ConversationId = ReadGuid(root, "conversationId");
            notification.MessageId = ReadGuid(root, "messageId");
            notification.Context = ReadContext(notification.Topic, root);
        }
        catch (JsonException)
        {
            // Historical payload metadata is best-effort and must never break the inbox.
        }
    }

    private static NotificationContextDto? ReadContext(TopicNotifications topic, JsonElement root)
    {
        var definition = NotificationTopicCatalog.GetDefinition(topic);
        var contextElement = TryGetProperty(root, "context", out var nestedContext) &&
            nestedContext.ValueKind == JsonValueKind.Object
                ? nestedContext
                : root;

        var aggregateType = ReadString(contextElement, "aggregateType") ?? definition.AggregateType;
        if (string.IsNullOrWhiteSpace(aggregateType))
        {
            return null;
        }

        var aggregateId = ReadGuid(contextElement, "aggregateId") ?? ReadAggregateId(root, aggregateType);
        var aggregateCode = ReadString(contextElement, "aggregateCode") ?? ReadAggregateCode(root, aggregateType);

        if (!aggregateId.HasValue && string.IsNullOrWhiteSpace(aggregateCode))
        {
            return null;
        }

        return new NotificationContextDto
        {
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            AggregateCode = aggregateCode
        };
    }

    private static Guid? ReadAggregateId(JsonElement root, string aggregateType)
    {
        var keys = aggregateType switch
        {
            "ProductSample" => new[] { "productSampleId", "sampleId" },
            "SampleRequest" => new[] { "sampleRequestId", "relatedId" },
            "MerchandiseOrder" => new[] { "merchandiseOrderId", "orderId", "saleOrderId", "relatedId" },
            "ManufacturingOrder" => new[] { "manufacturingOrderId", "relatedId" },
            "MfgProductionOrder" => new[] { "mfgProductionOrderId", "productionOrderId", "relatedId" },
            "ManufacturingFormula" => new[] { "manufacturingFormulaId", "formulaId", "relatedId" },
            "Quotation" => new[] { "quotationId", "relatedId" },
            "Customer" => new[] { "customerId", "relatedId" },
            "CustomerLead" => new[] { "leadId", "customerLeadId", "relatedId" },
            "WorkTask" => new[] { "workTaskId", "taskId", "relatedId" },
            "WorkPlan" => new[] { "workPlanId", "relatedId" },
            "ComplaintReport" => new[] { "complaintReportId", "complaintId", "relatedId" },
            "InternalConversation" => new[] { "conversationId" },
            _ => new[] { "relatedId" }
        };

        return keys.Select(key => ReadGuid(root, key)).FirstOrDefault(value => value.HasValue);
    }

    private static string? ReadAggregateCode(JsonElement root, string aggregateType)
    {
        var keys = aggregateType switch
        {
            "SampleRequest" => new[] { "sampleRequestExternalId", "relatedExternalId", "externalId" },
            "MerchandiseOrder" => new[] { "merchandiseOrderExternalId", "orderExternalId", "relatedExternalId", "externalId" },
            "ManufacturingOrder" => new[] { "manufacturingOrderExternalId", "relatedExternalId", "externalId" },
            "MfgProductionOrder" => new[] { "productionOrderExternalId", "relatedExternalId", "externalId" },
            "Quotation" => new[] { "quotationExternalId", "relatedExternalId", "externalId" },
            "Customer" => new[] { "customerExternalId", "relatedExternalId", "externalId" },
            "ComplaintReport" => new[] { "complaintExternalId", "relatedExternalId", "externalId" },
            _ => new[] { "relatedExternalId", "externalId" }
        };

        return keys.Select(key => ReadString(root, key)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static Guid? ReadGuid(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id)
            ? id
            : null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
