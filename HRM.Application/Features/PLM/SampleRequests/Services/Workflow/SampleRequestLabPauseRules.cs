using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

/// <summary>The latest refusal/resume event pauses the TP, never its shared VU.</summary>
internal static class SampleRequestLabPauseRules
{
    internal const string Rejected = "LabRequestPaused";
    internal const string Resumed = "SaleResumedLabRequest";
    internal const string PausedMessage = "Lab paused this request. Sale must reopen it with a reason before processing can continue.";
    internal static SampleRequestWorkflowEvent? Latest(IEnumerable<SampleRequestWorkflowEvent> events)
        => events.Where(e => e.EventType is Rejected or Resumed or SampleFollowUpRules.Started).OrderByDescending(e => e.RecordedAt)
            .ThenByDescending(e => e.Id).FirstOrDefault();
    internal static bool IsPaused(IEnumerable<SampleRequestWorkflowEvent> events) => Latest(events)?.EventType == Rejected;
    internal static Task<bool> IsPausedAsync(IPLMDbContext db, Guid company, Guid requestId, CancellationToken ct)
        => db.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == company &&
                e.SampleRequestId == requestId && (e.EventType == Rejected || e.EventType == Resumed || e.EventType == SampleFollowUpRules.Started))
            .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
            .Select(e => e.EventType == Rejected).FirstOrDefaultAsync(ct);
}
