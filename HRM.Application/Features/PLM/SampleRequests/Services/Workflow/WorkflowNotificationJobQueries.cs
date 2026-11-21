using HRM.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

public static class WorkflowNotificationJobQueries
{
    public const int MaximumAttempts = SampleRequestWorkflowNotificationQueue.MaxAttempts;
    // Each worker owns an explicit type. Claim under the same transaction as publication;
    // skip another worker's row instead of blocking behind it or consuming unknown types.
    public static IQueryable<OutboxMessage> ClaimPending(DbSet<OutboxMessage> jobs)
        => jobs.FromSqlInterpolated($"""
            SELECT * FROM notification.outbox_messages
            WHERE processed_at IS NULL AND attempts < {SampleRequestWorkflowNotificationQueue.MaxAttempts}
              AND (type={SampleRequestWorkflowNotificationQueue.OutboxType} OR type={SampleWorkRealtimeSignal.OutboxType})
            ORDER BY created_at, id LIMIT 1 FOR UPDATE SKIP LOCKED
            """);

    internal static IQueryable<OutboxMessage> Scoped(DbSet<OutboxMessage> jobs, Guid company, string? code)
        => jobs.FromSqlInterpolated($"""
            SELECT o.* FROM notification.outbox_messages o
            JOIN "SampleRequests"."SampleRequestWorkflowEvents" e ON e."Id"::text=o.payload_json->>'EventId'
            JOIN "SampleRequests"."SampleRequests" s ON s."SampleRequestId"=e."SampleRequestId"
            WHERE o.type={SampleRequestWorkflowNotificationQueue.OutboxType}
              AND o.payload_json->>'CompanyId'={company.ToString()} AND e."CompanyId"={company} AND s."CompanyId"={company}
              AND s."IsActive"=TRUE AND ({code ?? ""}='' OR s."ExternalId"={code ?? ""})
            """);

    internal static IQueryable<OutboxMessage> ReadyConfirmations(DbSet<OutboxMessage> jobs, Guid company, string? code)
        => jobs.FromSqlInterpolated($"""
            SELECT o.* FROM notification.outbox_messages o
            JOIN "SampleRequests"."SampleRequestWorkflowEvents" e ON e."Id"::text=o.payload_json->>'EventId'
            JOIN "SampleRequests"."SampleRequests" s ON s."SampleRequestId"=e."SampleRequestId"
            WHERE o.type={SampleRequestWorkflowNotificationQueue.OutboxType}
              AND o.payload_json->>'CompanyId'={company.ToString()} AND e."CompanyId"={company} AND s."CompanyId"={company}
              AND o.processed_at IS NOT NULL AND o.error='No handler for type SampleRequestWorkflowNotification'
              AND e."EventType"='LabFormulaConfirmed' AND e."MessageId" IS NULL AND e."NotificationId" IS NULL
              AND s."IsActive"=TRUE AND s."Status" NOT IN ('Completed','Cancelled','LabDeclined') AND s."FormulaId"=e."FormulaId"
              AND ({code ?? ""}='' OR s."ExternalId"={code ?? ""})
              AND NOT EXISTS (SELECT 1 FROM "SampleRequests"."SampleRequestWorkflowEvents" newer
                WHERE newer."CompanyId"=e."CompanyId" AND newer."SampleRequestId"=e."SampleRequestId"
                  AND newer."EventType"=e."EventType" AND newer."RecordedAt">e."RecordedAt")
              AND NOT EXISTS (SELECT 1 FROM "InternalMail"."InternalMessages" m WHERE m."InternalMessageId"=e."Id")
              AND NOT EXISTS (SELECT 1 FROM "SampleRequests"."SampleRequestWorkflowEvents" approved
                WHERE approved."CompanyId"=e."CompanyId" AND approved."SampleRequestId"=e."SampleRequestId"
                  AND approved."FormulaId"=e."FormulaId" AND approved."EventType"={SampleFlowRules.StandardPriceApproved}
                  AND approved."RecordedAt">=e."RecordedAt")
            """);

    internal static IQueryable<OutboxMessage> LockScoped(DbSet<OutboxMessage> jobs, Guid company, long jobId)
        => jobs.FromSqlInterpolated($"""
            SELECT o.* FROM notification.outbox_messages o
            JOIN "SampleRequests"."SampleRequestWorkflowEvents" e ON e."Id"::text=o.payload_json->>'EventId'
            JOIN "SampleRequests"."SampleRequests" s ON s."SampleRequestId"=e."SampleRequestId"
            WHERE o.id={jobId} AND o.type={SampleRequestWorkflowNotificationQueue.OutboxType}
              AND o.payload_json->>'CompanyId'={company.ToString()} AND e."CompanyId"={company} AND s."CompanyId"={company}
              AND s."IsActive"=TRUE
            FOR UPDATE OF o SKIP LOCKED
            """);
}
