using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

internal static class SaleRequestedFinalizationRules
{
    internal static string? Validate(SampleRequest request, Guid? formulaId, string action,
        IReadOnlyList<SampleRequestWorkflowEvent> events, IReadOnlyCollection<SampleRequestSampleTrial> trials)
    {
        var expected = action == "FinalizeQuotation" ? SampleFlowRules.LabFinalizationRequested : SampleFlowRules.ChipFinalizationRequested;
        var instruction = events.FirstOrDefault(e => e.EventType is SampleFlowRules.LabFinalizationRequested or
            SampleFlowRules.ChipFinalizationRequested or SampleFlowRules.ChipRequested or SampleFlowRules.KgRequested or
            SampleFlowRules.ChipThenSampleRequested or SampleFlowRules.CompletionDeferred or SampleFlowRules.ChipFollowUpDeferred);
        if (!SampleFlowRules.IsOpen(request.Status) || formulaId is null || instruction?.EventType != expected ||
            instruction.FormulaId != formulaId)
            return "The Sale finalization request no longer matches this formula. Reload before completing.";
        if ((action == "FinalizeQuotation" || KgSampleCompletionRules.RequiresPriceAcceptance(request, events)) &&
            !SampleFlowStateReader.ActivePrice(events, formulaId))
            return "Customer price must still be accepted for this request before finalizing it.";
        var current = trials.Where(t => t.IsActive && t.SampleRequestId == request.SampleRequestId).ToArray();
        if (request.SampleQuantity > 0 || current.Any(t => SampleFlowRules.IsPending(t.Status)) ||
            events.TakeWhile(e => e.Id != instruction.Id).Any(e => e.EventType == SampleRequestWorkflowEvents.SampleSent))
            return "Requested delivery or sample feedback is still pending. Reload before completing.";
        if (action == "FinalizeChip")
        {
            var chip = current.OrderByDescending(t => t.TrialNo).FirstOrDefault();
            if (chip is not { DeliveryKind: SampleDeliveryKind.ColorChip, SentDate: not null, Status: SampleTrialStatus.Approved } ||
                chip.FormulaId != formulaId || instruction.SampleRequestSampleTrialId != chip.SampleRequestSampleTrialId)
                return "Chip acceptance must match the formula and delivery selected by Sale.";
        }
        return null;
    }
}
