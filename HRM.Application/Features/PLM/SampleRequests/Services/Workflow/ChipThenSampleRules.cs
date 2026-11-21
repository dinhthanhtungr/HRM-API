using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

internal static class ChipThenSampleRules
{
    public const string Action = "RequestChipThenSample";

    public static SampleRequestWorkflowEvent? LatestPlan(IReadOnlyList<SampleRequestWorkflowEvent> events)
        => events.FirstOrDefault(e => e.EventType is SampleFlowRules.ChipThenSampleRequested or
            SampleFlowRules.ChipRequested or SampleFlowRules.KgRequested or SampleFlowRules.LabFinalizationRequested or
            SampleFlowRules.ChipFinalizationRequested or SampleFlowRules.CompletionDeferred);

    // Missing chip evidence is recorded alongside kg, never a separate confirmation step.
    public static bool NeedsChipRecord(SampleRequest request, Guid? formulaId,
        IReadOnlyList<SampleRequestWorkflowEvent> events, IEnumerable<SampleRequestSampleTrial> trials)
    {
        if (SampleFollowUpRules.IsLegacy(request, events) || request.SampleQuantity is not > 0) return false;
        var plan = LatestPlan(events);
        return plan?.EventType == SampleFlowRules.ChipThenSampleRequested &&
            !trials.Any(t => t.IsActive && t.SampleRequestId == request.SampleRequestId &&
                t.DeliveredSampleQuantityKg == 0 &&
                (t.CreatedDate >= plan.RecordedAt || events.Any(e => e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent &&
                    e.SampleRequestSampleTrialId == t.SampleRequestSampleTrialId && e.RecordedAt >= plan.RecordedAt)) && t.SentDate.HasValue);
    }

    public static bool CanContinueWithSample(SampleRequest request, SampleRequestSampleTrial? latest,
        IReadOnlyList<SampleRequestWorkflowEvent> events, IEnumerable<SampleRequestSampleTrial> trials)
        => !SampleFollowUpRules.IsLegacy(request, events) && request.SampleQuantity > 0 &&
            LatestPlan(events)?.EventType == SampleFlowRules.ChipThenSampleRequested &&
            latest is { DeliveredSampleQuantityKg: 0, SentDate: not null } &&
            !NeedsChipRecord(request, latest.FormulaId, events, trials);

    public static IQueryable<SampleRequestSampleTrial> OptionalChips(IQueryable<SampleRequestSampleTrial> trials)
        => trials.Where(t => t.IsActive && t.DeliveredSampleQuantityKg == 0 && t.SentDate != null &&
            (t.SampleRequest.CreatedDate >= SampleFlowRules.StartsAt || t.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == t.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) && trials.Any(next => next.IsActive &&
                next.SampleRequestId == t.SampleRequestId && next.TrialNo > t.TrialNo &&
                next.DeliveredSampleQuantityKg > 0 && next.SentDate != null));

    public static IQueryable<SampleRequestSampleTrial> PlannedChips(IQueryable<SampleRequestSampleTrial> trials,
        IQueryable<SampleRequestWorkflowEvent> events)
        => trials.Where(t => t.IsActive && t.DeliveredSampleQuantityKg == 0 && t.SentDate != null &&
            (t.SampleRequest.CreatedDate >= SampleFlowRules.StartsAt || t.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == t.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) && t.SampleRequest.SampleQuantity > 0 &&
            events.Where(e => e.CompanyId == t.SampleRequest.CompanyId && e.SampleRequestId == t.SampleRequestId &&
                (e.EventType == SampleFlowRules.ChipThenSampleRequested || e.EventType == SampleFlowRules.ChipRequested ||
                 e.EventType == SampleFlowRules.KgRequested || e.EventType == SampleFlowRules.LabFinalizationRequested ||
                 e.EventType == SampleFlowRules.ChipFinalizationRequested || e.EventType == SampleFlowRules.CompletionDeferred))
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                .Select(e => e.EventType == SampleFlowRules.ChipThenSampleRequested &&
                    (t.CreatedDate >= e.RecordedAt || events.Any(sent => sent.CompanyId == e.CompanyId &&
                        sent.SampleRequestId == e.SampleRequestId && sent.SampleRequestSampleTrialId == t.SampleRequestSampleTrialId &&
                        sent.EventType == Rules.SampleRequestWorkflowEvents.SampleSent && sent.RecordedAt >= e.RecordedAt))).FirstOrDefault());

    public static IQueryable<SampleRequestSampleTrial> BlockingPending(IQueryable<SampleRequestSampleTrial> trials, bool allowChipContinuation = false)
        => trials.Where(t => t.IsActive && (t.Status == SampleTrialStatus.SampleSent ||
            t.Status == SampleTrialStatus.WaitingCustomerFeedback || t.Status == SampleTrialStatus.PriceQuote) &&
            !(t.SentDate != null && t.SampleRequest.WorkflowEvents.Any(plan =>
                plan.CompanyId == t.SampleRequest.CompanyId && plan.EventType == SampleFlowRules.KgRequested &&
                plan.RecordedAt > t.CreatedDate &&
                !t.SampleRequest.WorkflowEvents.Any(sent => sent.CompanyId == plan.CompanyId &&
                    sent.EventType == Rules.SampleRequestWorkflowEvents.SampleSent && sent.SampleRequestSampleTrialId == t.SampleRequestSampleTrialId &&
                    sent.RecordedAt >= plan.RecordedAt))) &&
            !(t.DeliveredSampleQuantityKg == 0 && t.SentDate != null && (t.SampleRequest.CreatedDate >= SampleFlowRules.StartsAt || t.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == t.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) &&
                (allowChipContinuation || trials.Any(next => next.IsActive && next.SampleRequestId == t.SampleRequestId &&
                    next.TrialNo > t.TrialNo && next.DeliveredSampleQuantityKg > 0 && next.SentDate != null))));

    public static bool IsBeforeSampleRequest(SampleRequestSampleTrial trial, IReadOnlyList<SampleRequestWorkflowEvent>? events)
    {
        // An explicit repeat request releases the earlier delivery lock, not its feedback/history.
        // Later plan choices must not make that historical delivery block the TP again.
        return trial.SentDate.HasValue && events is not null && events.Any(plan =>
            plan.EventType == SampleFlowRules.KgRequested && plan.SampleRequestId == trial.SampleRequestId &&
            trial.CreatedDate < plan.RecordedAt && !events.Any(sent =>
                sent.CompanyId == plan.CompanyId && sent.SampleRequestId == plan.SampleRequestId &&
                sent.EventType == Rules.SampleRequestWorkflowEvents.SampleSent &&
                sent.SampleRequestSampleTrialId == trial.SampleRequestSampleTrialId && sent.RecordedAt >= plan.RecordedAt));
    }

    public static bool HasBlockingPending(IEnumerable<SampleRequestSampleTrial> trials, bool allowChipContinuation = false,
        IReadOnlyList<SampleRequestWorkflowEvent>? events = null)
        => trials.Any(t => t.IsActive && SampleFlowRules.IsPending(t.Status) &&
            !IsBeforeSampleRequest(t, events) &&
            !(t.DeliveredSampleQuantityKg == 0 && t.SentDate != null && (allowChipContinuation ||
                trials.Any(next => next.IsActive && next.SampleRequestId == t.SampleRequestId && next.TrialNo > t.TrialNo &&
                    next.DeliveredSampleQuantityKg > 0 && next.SentDate != null))));
}
