using System.Text.Json;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Notifications.Services;

internal static class NotificationDeliveryStatusRules
{
    public static IQueryable<Notification> Filter(IQueryable<Notification> source) => source.Where(n =>
        n.Topic == TopicNotifications.MerchandiseOrderDeliveryPaused || n.Topic == TopicNotifications.MerchandiseOrderDeliveryResumed ||
        n.Topic == TopicNotifications.MerchandiseOrderUpdated && n.PayloadJson != null &&
        (EF.Functions.JsonContains(n.PayloadJson, "{\"isDeliveryPaused\":true}") || EF.Functions.JsonContains(n.PayloadJson, "{\"isDeliveryPaused\":false}")) &&
        EF.Functions.JsonExists(n.PayloadJson, "merchandiseOrderId") && EF.Functions.JsonExists(n.PayloadJson, "merchandiseOrderCode") &&
        EF.Functions.JsonExists(n.PayloadJson, "deliveryPausedFrom") && EF.Functions.JsonExists(n.PayloadJson, "deliveryPausedTo") &&
        EF.Functions.JsonExists(n.PayloadJson, "deliveryPauseReason") && EF.Functions.JsonExists(n.PayloadJson, "deliveryPauseType"));

    public static bool IsLegacyPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("isDeliveryPaused", out var paused) &&
                paused.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                new[] { "merchandiseOrderId", "merchandiseOrderCode", "deliveryPausedFrom", "deliveryPausedTo", "deliveryPauseReason", "deliveryPauseType" }
                    .All(key => root.TryGetProperty(key, out _));
        }
        catch (JsonException) { return false; }
    }
}
