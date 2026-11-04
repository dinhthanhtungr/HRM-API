using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

internal static class SampleRequestWorkflowNotificationQueue
{
    internal const string OutboxType = "SampleRequestWorkflowNotification";
    internal const int MaxAttempts = 5;

    public static void Queue(IPLMDbContext db, SampleRequestWorkflowEvent evt, SendSampleRequestMessageCommand? message = null,
        bool materialsChanged = false)
        => Queue(db.OutboxMessages, evt, message, materialsChanged);

    public static void Queue(DbSet<OutboxMessage> outbox, SampleRequestWorkflowEvent evt, SendSampleRequestMessageCommand? message = null,
        bool materialsChanged = false)
    {
        var payload = new WorkflowNotificationPayload(evt.Id, evt.CompanyId,
            message?.Message, message?.Type ?? SampleRequestNotificationType.GeneralMessage,
            message?.IsInitialMessage ?? false, message?.NotificationRecipientEmployeeIdsOverride?.ToArray(),
            message?.SilentWatcherEmployeeIds?.ToArray(), message?.UseDefaultSilentWatchersWhenOmitted ?? false,
            message?.TopicOverride, message?.TitleOverride, message?.ExtraRecipientEmployeeIds.ToArray(), materialsChanged);
        outbox.Add(new OutboxMessage
        {
            Type = OutboxType, PayloadJson = JsonSerializer.Serialize(payload), CreatedAt = evt.RecordedAt
        });
    }
}

internal sealed record WorkflowNotificationPayload(Guid EventId, Guid CompanyId, string? Message = null,
    SampleRequestNotificationType Type = SampleRequestNotificationType.GeneralMessage,
    bool IsInitialMessage = false, Guid[]? Recipients = null, Guid[]? SilentWatchers = null,
    bool UseDefaultSilentWatchers = false, TopicNotifications? Topic = null, string? Title = null, Guid[]? ExtraRecipients = null,
    bool MaterialsChanged = false);
