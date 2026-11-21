using System.Globalization;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetWorkflowNotificationJobs;

internal sealed class GetWorkflowNotificationJobsQueryHandler(IPLMReadDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions)
    : IRequestHandler<GetWorkflowNotificationJobsQuery, PagedResult<WorkflowNotificationJobDto>?>
{
    public async Task<PagedResult<WorkflowNotificationJobDto>?> Handle(GetWorkflowNotificationJobsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is null ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs))
            return null;

        var code = request.SampleRequestCode?.Trim();
        if (!WorkflowNotificationJobRules.Filters.Contains(request.Status) || code?.Length > 80)
            throw new ArgumentException("Invalid workflow notification job filter.");
        var now = DateTime.Now;
        var delayedBefore = now.AddMinutes(-WorkflowNotificationJobRules.DelayedMinutes);
        var jobs = WorkflowNotificationJobQueries.Scoped(db.OutboxMessages, company, code).AsNoTracking();
        jobs = request.Status switch
        {
            "Attention" => jobs.Where(x => x.ProcessedAt != null && x.Error != null || x.ProcessedAt == null && (x.Attempts >= SampleRequestWorkflowNotificationQueue.MaxAttempts || x.CreatedAt <= delayedBefore)),
            "Failed" => jobs.Where(x => x.ProcessedAt == null && x.Attempts >= SampleRequestWorkflowNotificationQueue.MaxAttempts),
            "Delayed" => jobs.Where(x => x.ProcessedAt == null && x.Attempts < SampleRequestWorkflowNotificationQueue.MaxAttempts && x.CreatedAt <= delayedBefore),
            "Pending" => jobs.Where(x => x.ProcessedAt == null && x.Attempts < SampleRequestWorkflowNotificationQueue.MaxAttempts && x.CreatedAt > delayedBefore),
            "Completed" => jobs.Where(x => x.ProcessedAt != null && x.Error == null),
            _ => jobs
        };
        var total = await jobs.CountAsync(ct);
        var page = Math.Clamp(request.PageNumber, 1, 1000000);
        var size = Math.Clamp(request.PageSize, 1, 50);
        var rows = await jobs.OrderBy(x => x.ProcessedAt != null).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var eventIds = rows.Select(x => WorkflowNotificationJobRules.EventId(x.PayloadJson)).ToArray();
        var details = await (from evt in db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company && eventIds.Contains(x.Id))
                             join sample in db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == company)
                                 on evt.SampleRequestId equals sample.SampleRequestId
                             select new { Event = evt, sample.ExternalId }).ToDictionaryAsync(x => x.Event.Id, ct);
        var mayRetry = permissions.HasPermission(ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs);
        var assessments = await WorkflowNotificationReplayAssessment.ReadAsync(db, company,
            details.Values.Select(x => x.Event).ToArray(), ct);
        var items = rows.Select(job =>
        {
            var eventId = WorkflowNotificationJobRules.EventId(job.PayloadJson);
            if (!details.TryGetValue(eventId, out var detail)) return null;
            return new WorkflowNotificationJobDto(job.Id.ToString(CultureInfo.InvariantCulture), eventId,
                detail.Event.SampleRequestId, detail.ExternalId, detail.Event.EventType,
                WorkflowNotificationJobRules.Status(job.ProcessedAt, job.Attempts, job.CreatedAt, now, job.Error),
                job.CreatedAt, job.ProcessedAt, job.Attempts, SampleRequestWorkflowNotificationQueue.MaxAttempts,
                WorkflowNotificationJobRules.SafeError(job.Error), mayRetry && (WorkflowNotificationJobRules.CanRetry(
                    job.ProcessedAt, job.Attempts, detail.Event.NotificationId) || WorkflowNotificationJobRules.CanRecoverMissingHandler(
                    job.ProcessedAt, job.Error, detail.Event.MessageId, detail.Event.NotificationId)),
                mayRetry && job.ProcessedAt.HasValue && job.Error == "No handler for type SampleRequestWorkflowNotification" &&
                    detail.Event.EventType == "SampleRequestWorkflowCreated" && detail.Event.MessageId is null &&
                    detail.Event.NotificationId is null && WorkflowNotificationJobRules.InitialSummary(job.PayloadJson) is not null,
                detail.Event.MessageId.HasValue, mayRetry && detail.Event.EventType == "SampleRequestWorkflowCreated",
                assessments.GetValueOrDefault(eventId));
        }).OfType<WorkflowNotificationJobDto>().ToArray();
        return new PagedResult<WorkflowNotificationJobDto>(items, total, page, size);
    }
}
