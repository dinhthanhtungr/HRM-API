using HRM.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

internal static class WorkflowNotificationJobQueries
{
    internal static IQueryable<OutboxMessage> Scoped(DbSet<OutboxMessage> jobs, Guid company, string? code)
        => jobs.FromSqlInterpolated($"""
            SELECT o.* FROM notification.outbox_messages o
            JOIN "SampleRequests"."SampleRequestWorkflowEvents" e ON e."Id"::text=o.payload_json->>'EventId'
            JOIN "SampleRequests"."SampleRequests" s ON s."SampleRequestId"=e."SampleRequestId"
            WHERE o.type={SampleRequestWorkflowNotificationQueue.OutboxType}
              AND o.payload_json->>'CompanyId'={company.ToString()} AND e."CompanyId"={company} AND s."CompanyId"={company}
              AND s."IsActive"=TRUE AND ({code ?? ""}='' OR s."ExternalId"={code ?? ""})
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
