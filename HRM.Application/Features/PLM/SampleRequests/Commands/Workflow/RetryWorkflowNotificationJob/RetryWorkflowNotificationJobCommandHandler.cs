using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RetryWorkflowNotificationJob;

internal sealed class RetryWorkflowNotificationJobCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ILogger<RetryWorkflowNotificationJobCommandHandler> logger)
    : IRequestHandler<RetryWorkflowNotificationJobCommand, RetryWorkflowNotificationJobResult>
{
    public async Task<RetryWorkflowNotificationJobResult> Handle(RetryWorkflowNotificationJobCommand request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is not { } actor ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs) ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs))
            return new("Forbidden");

        await using var transaction = await db.BeginTransactionAsync(ct);
        // The worker locks this same row. SKIP LOCKED avoids waiting for an active dispatch and never steals it.
        var rows = await WorkflowNotificationJobQueries.LockScoped(db.OutboxMessages, company, request.JobId).ToListAsync(ct);
        var job = rows.SingleOrDefault();
        if (job is null) return new("Unavailable");
        var eventId = WorkflowNotificationJobRules.EventId(job.PayloadJson);
        var evt = await db.SampleRequestWorkflowEvents.AsNoTracking().SingleAsync(x => x.Id == eventId && x.CompanyId == company, ct);
        var recover = WorkflowNotificationJobRules.CanRecoverMissingHandler(job.ProcessedAt, job.Error, evt.MessageId, evt.NotificationId);
        if (recover && await db.InternalMessages.AsNoTracking().AnyAsync(x => x.InternalMessageId == eventId, ct))
            return new("RecoveryConflict");
        if (!recover && (job.ProcessedAt.HasValue || evt.NotificationId.HasValue)) return new("Completed");
        if (!recover && !WorkflowNotificationJobRules.CanRetry(job.ProcessedAt, job.Attempts, evt.NotificationId)) return new("AlreadyQueued");
        var previousAttempts = job.Attempts;
        job.ProcessedAt = null;
        job.Attempts = 0;
        job.Error = null;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Employee {ActorId} requeued workflow notification job {JobId} in company {CompanyId}; previous attempts {Attempts}",
            actor, job.Id, company, previousAttempts);
        return new("Queued");
    }
}
