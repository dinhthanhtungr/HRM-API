using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

/// <summary>Secondary Sale choices do not replace the task assigned to Lab/BGD.</summary>
internal static class SampleRequestPlanRules
{
    internal static IReadOnlyList<string> Actions(SampleRequest request, SampleRequestSampleTrial? trial,
        Formula? formula, bool sale, bool labConfirmed)
    {
        if (!sale) return [];
        var actions = new List<string>();
        actions.Add("RequestSample");
        if (trial == null || !SampleFlowRules.IsPending(trial.Status)) actions.AddRange(["RequestChip", ChipThenSampleRules.Action]);
        if (labConfirmed && formula?.Status is "Approved" or "Completed" or "SampleSent") actions.Add("RequestRequote");
        return actions;
    }

    // Delivery plans stay on the same TP; sent trials are immutable history.
    internal static bool NeedsNewRequest(SampleRequestSampleTrial? trial, bool hasSentKg) => false;

    internal static bool HasPendingPlan(SampleRequest request, IReadOnlyList<SampleRequestWorkflowEvent> events,
        IReadOnlyCollection<SampleRequestSampleTrial> trials)
    {
        var match = System.Text.RegularExpressions.Regex.Match(request.InfoType ?? "", @"\(\s*(\d+)\s*\)");
        int? sets = match.Success && int.TryParse(match.Groups[1].Value, out var count) ? count : null;
        return new[] { "RequestChip", "RequestSample", ChipThenSampleRules.Action }.Any(action =>
            IsUnchangedPendingPlan(request, events, trials, action, request.SampleQuantity, sets));
    }

    internal static bool IsUnchangedPendingPlan(SampleRequest request,
        IReadOnlyList<SampleRequestWorkflowEvent> events, IReadOnlyCollection<SampleRequestSampleTrial> trials,
        string action, double? kg, int? sets)
    {
        if (request.Status is "Completed" or "Cancelled" or "LabDeclined" ||
            action is not ("RequestChip" or "RequestSample" or ChipThenSampleRules.Action)) return false;
        var plan = ChipThenSampleRules.LatestPlan(events);
        if (plan is null && SampleFollowUpRules.IsLegacy(request, events)) return false;
        var sent = trials.Where(t => t.IsActive && t.SentDate.HasValue).ToArray();
        var sentAfterPlan = sent.Where(t => plan == null || t.CreatedDate >= plan.RecordedAt ||
            events.Any(e => e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent &&
                e.SampleRequestSampleTrialId == t.SampleRequestSampleTrialId && e.RecordedAt >= plan.RecordedAt)).ToArray();
        var expectedAction = plan?.EventType switch
        {
            SampleFlowRules.ChipRequested => "RequestChip",
            SampleFlowRules.KgRequested => "RequestSample",
            SampleFlowRules.ChipThenSampleRequested => ChipThenSampleRules.Action,
            null when request.SampleQuantity > 0 => "RequestSample",
            null when request.InfoType?.Trim().StartsWith("Thẻ màu", StringComparison.OrdinalIgnoreCase) == true ||
                request.InfoType?.Trim().StartsWith("ColorSampleSet", StringComparison.OrdinalIgnoreCase) == true => "RequestChip",
            _ => null
        };
        if (expectedAction != action) return false;
        // The chip part does not fulfil a combined plan; the kg delivery does.
        if (sentAfterPlan.Any(t => action != ChipThenSampleRules.Action || t.DeliveredSampleQuantityKg > 0)) return false;
        if (action is "RequestSample" or ChipThenSampleRules.Action && kg != request.SampleQuantity) return false;
        if (action is "RequestChip" or ChipThenSampleRules.Action)
        {
            var match = System.Text.RegularExpressions.Regex.Match(request.InfoType ?? "", @"\(\s*(\d+)\s*\)");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var savedSets) || savedSets != sets) return false;
        }
        return true;
    }

    internal static bool HasOutstandingDelivery(IReadOnlyList<SampleRequestWorkflowEvent> events)
    {
        var plan = ChipThenSampleRules.LatestPlan(events);
        return plan?.EventType is SampleFlowRules.ChipRequested or SampleFlowRules.KgRequested or SampleFlowRules.ChipThenSampleRequested &&
            !events.Any(e => e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent && e.RecordedAt >= plan.RecordedAt);
    }
}
