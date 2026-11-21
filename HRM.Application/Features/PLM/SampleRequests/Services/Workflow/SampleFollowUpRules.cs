using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

/// <summary>Explicit new work opts an old TP into the current lifecycle without rewriting its history.</summary>
internal static class SampleFollowUpRules
{
    public const string Started = "SampleRequestFollowUpStarted";
    public const string RequoteRequested = "FormulaRequoteRequested";

    public static bool IsDeliveryPlan(string action)
        => action is "RequestChip" or "RequestSample" or ChipThenSampleRules.Action;

    public static bool IsLegacy(SampleRequest request, IEnumerable<SampleRequestWorkflowEvent>? events)
        => SampleFlowRules.IsLegacy(request.CreatedDate) &&
            !(events ?? []).Any(e => e.CompanyId == request.CompanyId && e.SampleRequestId == request.SampleRequestId && e.EventType == Started);

    public static Task<bool> IsLegacyAsync(IPLMDbContext db, SampleRequest request, CancellationToken ct)
        => IsLegacyAsync(db.SampleRequestWorkflowEvents, request, ct);

    public static async Task<bool> IsLegacyAsync(IQueryable<SampleRequestWorkflowEvent> events, SampleRequest request, CancellationToken ct)
        => SampleFlowRules.IsLegacy(request.CreatedDate) && !await events.AsNoTracking()
            .AnyAsync(e => e.CompanyId == request.CompanyId && e.SampleRequestId == request.SampleRequestId && e.EventType == Started, ct);
}
