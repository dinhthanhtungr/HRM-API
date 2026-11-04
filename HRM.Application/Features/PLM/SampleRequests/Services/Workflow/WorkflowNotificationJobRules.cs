using HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;
using System.Text.Json;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

internal static class WorkflowNotificationJobRules
{
    internal const int DelayedMinutes = 10;
    internal static readonly string[] Filters = ["Attention", "Failed", "Delayed", "Pending", "Completed", "All"];

    internal static string Status(DateTime? processedAt, int attempts, DateTime createdAt, DateTime now, string? error = null)
        => processedAt.HasValue ? error is not null ? "NeedsReview" : "Completed" : attempts >= SampleRequestWorkflowNotificationQueue.MaxAttempts
            ? "Failed" : createdAt <= now.AddMinutes(-DelayedMinutes) ? "Delayed" : "Pending";

    internal static bool CanRetry(DateTime? processedAt, int attempts, Guid? notificationId)
        => !processedAt.HasValue && !notificationId.HasValue && attempts >= SampleRequestWorkflowNotificationQueue.MaxAttempts;

    internal static bool CanRecoverMissingHandler(DateTime? processedAt, string? error, Guid? messageId, Guid? notificationId)
        => processedAt.HasValue && error == "No handler for type SampleRequestWorkflowNotification" &&
            !messageId.HasValue && !notificationId.HasValue;

    internal static Guid EventId(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.GetProperty("EventId").GetGuid();
    }

    internal static string? InitialSummary(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.TryGetProperty("IsInitialMessage", out var initial) && initial.ValueKind == JsonValueKind.True &&
            json.RootElement.TryGetProperty("Message", out var message) && message.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(message.GetString()) ? message.GetString() : null;
    }

    // Never return exception text or payload content from older outbox rows.
    internal static string? SafeError(string? error)
    {
        if (error is null) return null;
        if (error == "No handler for type SampleRequestWorkflowNotification")
            return "WorkflowNotification.LegacyHandlerMissing";
        const string prefix = "WorkflowNotification.";
        return error.StartsWith(prefix, StringComparison.Ordinal) &&
            Enum.TryParse<WorkflowNotificationFailureCode>(error[prefix.Length..], out var code) && Enum.IsDefined(code)
            ? prefix + code : "WorkflowNotification.Unknown";
    }
}
