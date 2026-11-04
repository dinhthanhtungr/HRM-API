using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RestoreWorkflowSummary;

/// <summary>Restore saved initial summary history only; never replay workflow, notification or unread side effects.</summary>
internal sealed class RestoreWorkflowSummaryCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ILogger<RestoreWorkflowSummaryCommandHandler> logger, ISender dispatcher)
    : IRequestHandler<RestoreWorkflowSummaryCommand, RetryWorkflowNotificationJobResult>
{
    public async Task<RetryWorkflowNotificationJobResult> Handle(RestoreWorkflowSummaryCommand request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is not { } actor ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs) ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs)) return new("Forbidden");

        await using var transaction = await db.BeginTransactionAsync(ct);
        var job = (await WorkflowNotificationJobQueries.LockScoped(db.OutboxMessages, company, request.JobId).ToListAsync(ct)).SingleOrDefault();
        if (job is null) return new("Unavailable");
        if (!job.ProcessedAt.HasValue || job.Error != "No handler for type SampleRequestWorkflowNotification") return new("RecoveryConflict");
        var body = WorkflowNotificationJobRules.InitialSummary(job.PayloadJson);
        if (body is null) return new("RecoverySourceMissing");
        var eventId = WorkflowNotificationJobRules.EventId(job.PayloadJson);
        var events = await db.SampleRequestWorkflowEvents.FromSqlInterpolated($"""
            SELECT * FROM "SampleRequests"."SampleRequestWorkflowEvents"
            WHERE "Id"={eventId} AND "CompanyId"={company} FOR UPDATE
            """).ToListAsync(ct);
        var evt = events.SingleOrDefault();
        if (evt is null || evt.EventType != "SampleRequestWorkflowCreated" || evt.RecordedByEmployeeId is not { } sender ||
            evt.NotificationId.HasValue || (evt.MessageId.HasValue && evt.MessageId != eventId)) return new("RecoveryConflict");
        var sample = await db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && x.SampleRequestId == evt.SampleRequestId)
            .Select(x => new { x.SampleRequestId, x.CreatedDate, x.ExternalId }).SingleOrDefaultAsync(ct);
        if (sample is null || !await db.Employees.AnyAsync(x => x.CompanyId == company && x.EmployeeId == sender, ct)) return new("RecoveryConflict");
        var roots = await db.InternalConversations.FromSqlInterpolated($"""
            SELECT * FROM "InternalMail"."InternalConversations" WHERE "CompanyId"={company}
              AND "RelatedId"={evt.SampleRequestId} AND "RelatedType"={(int)InternalMailRelatedType.SampleRequest} AND "IsActive"=TRUE
            FOR UPDATE
            """).ToListAsync(ct);
        if (roots.Count > 1) return new("RecoveryMultipleConversations");
        if (roots.Count == 0)
        {
            // Never recreate a removed conversation or move an existing message into a new root.
            if (evt.ConversationId.HasValue || evt.MessageId.HasValue ||
                await db.InternalConversations.AnyAsync(x => x.CompanyId == company && x.RelatedId == evt.SampleRequestId &&
                    x.RelatedType == InternalMailRelatedType.SampleRequest, ct) ||
                await db.InternalMessages.AnyAsync(x => x.InternalMessageId == eventId, ct))
                return new("RecoveryConflict");
            var payload = JsonSerializer.Deserialize<WorkflowNotificationPayload>(job.PayloadJson)!;
            var restored = await dispatcher.Send(new SendSampleRequestMessageCommand
            {
                SampleRequestId = evt.SampleRequestId, WorkflowEventId = eventId,
                IsInitialMessage = true, DeferNotification = true, RestoreInitialSummaryHistory = true,
                Message = body, Type = payload.Type,
                ExtraRecipientEmployeeIds = payload.ExtraRecipients ?? [],
                SilentWatcherEmployeeIds = payload.SilentWatchers,
                UseDefaultSilentWatchersWhenOmitted = payload.UseDefaultSilentWatchers
            }, ct);
            if (!restored.Success || restored.Data is null || restored.Data.MessageId == Guid.Empty)
                return new("RecoveryConversationUnavailable");
            evt.MessageId = restored.Data.MessageId;
            evt.ConversationId = restored.Data.ConversationId;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Employee {ActorId} restored initial conversation summary for job {JobId} in company {CompanyId}", actor, job.Id, company);
            return new("Restored");
        }
        var root = roots[0];
        if (evt.ConversationId.HasValue && evt.ConversationId != root.InternalConversationId) return new("RecoveryConflict");
        var existing = await db.InternalMessages.AsNoTracking().SingleOrDefaultAsync(x => x.InternalMessageId == eventId, ct);
        if (existing is not null)
        {
            if (existing.InternalConversationId != root.InternalConversationId || existing.IsDeleted ||
                existing.Body != body || existing.SenderEmployeeId != sender || existing.SentAt != sample.CreatedDate)
                return new("RecoveryConflict");
            evt.MessageId = eventId;
            evt.ConversationId = root.InternalConversationId;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new("AlreadyRestored");
        }
        // Reject a summary imported with a different ID as well as deleted or metadata-linked copies.
        var duplicates = await db.InternalMessages.FromSqlInterpolated($"""
            SELECT * FROM "InternalMail"."InternalMessages" WHERE "InternalConversationId"={root.InternalConversationId}
             AND ("Body"={body} OR "PayloadJson"->>'workflowEventId'={eventId.ToString()}
                  OR "PayloadJson"->>'WorkflowEventId'={eventId.ToString()})
            """).AsNoTracking().AnyAsync(ct);
        if (duplicates) return new("RecoveryConflict");
        db.InternalMessages.Add(new InternalMessage
        {
            InternalMessageId = eventId, InternalConversationId = root.InternalConversationId,
            SenderEmployeeId = sender, Body = body, SentAt = sample.CreatedDate, MessageType = InternalMessageType.Text,
            PayloadJson = JsonSerializer.Serialize(new SampleRequestThreadMessagePayload
            {
                ConversationId = root.InternalConversationId, MessageId = eventId, SampleRequestId = sample.SampleRequestId,
                WorkflowEventId = eventId, ExternalId = sample.ExternalId, Type = "GeneralMessage", SaleMessage = body
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        });
        evt.MessageId = eventId;
        evt.ConversationId = root.InternalConversationId;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Employee {ActorId} restored initial summary for job {JobId} in company {CompanyId}", actor, job.Id, company);
        return new("Restored");
    }
}
