using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>Phạm vi vận hành cố định; không loại việc tồn khi sang tháng/năm mới.</summary>
internal static class SampleRequestWorkScope
{
    public static readonly DateTime StartedAt = new(2026, 11, 1);
    public const string LegacyIncluded = "LegacyWorkTrackingIncluded";
    public const string LegacyExcluded = "LegacyWorkTrackingExcluded";

    public static IQueryable<SampleRequestWorkflowEvent> TrackingEvents(
        IQueryable<SampleRequestWorkflowEvent> events) => events.Where(x =>
        x.EventType == LegacyIncluded || x.EventType == LegacyExcluded);

    public static IQueryable<SampleRequest> Apply(IQueryable<SampleRequest> requests,
        IQueryable<SampleRequestWorkflowEvent> events)
    {
        var tracking = TrackingEvents(events);
        return requests.Where(request => request.CreatedDate >= StartedAt ||
            request.WorkflowEvents.Any(e => e.CompanyId == request.CompanyId && e.EventType == Services.Workflow.SampleFollowUpRules.Started) ||
            tracking.Where(e => e.CompanyId == request.CompanyId && e.SampleRequestId == request.SampleRequestId)
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                .Select(e => e.EventType).FirstOrDefault() == LegacyIncluded);
    }
}
