using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
internal static class SampleDeliveryRules
{
    // Candidate availability enables an explicit Lab choice, never a historical TP/VU link.
    internal static IQueryable<Guid> RequestsWithUnsentConfirmedFormula(IQueryable<SampleRequest> requests,
        IQueryable<Formula> formulas, IQueryable<SampleRequestSampleTrial> trials)
        => requests.Where(r => r.IsActive && r.Product.IsActive && r.Product.CompanyId == r.CompanyId && formulas.Any(f => f.IsActive &&
            f.CompanyId == r.CompanyId && f.ProductId == r.ProductId && (f.Status == "Approved" || f.Status == "Completed") &&
            !trials.Any(t => t.IsActive && t.FormulaId == f.FormulaId && t.SampleRequest.CompanyId == r.CompanyId &&
                t.DeliveredSampleQuantityKg > 0 && t.SentDate != null))).Select(r => r.SampleRequestId);

    internal static IQueryable<Formula> SendableFormulas(IQueryable<Formula> formulas,
        IQueryable<SampleRequestSampleTrial> trials, SampleRequest request, Guid? retryFormulaId)
        => formulas.Where(f => f.CompanyId == request.CompanyId && f.ProductId == request.ProductId && f.IsActive && f.Product.IsActive &&
            (f.Status == "Approved" || f.Status == "Draft" || f.Status == "Completed" || f.Status == "SampleSent" && trials.Any(t => t.IsActive &&
                t.SampleRequestId == request.SampleRequestId && t.FormulaId == f.FormulaId &&
                t.SentDate != null && t.DeliveredSampleQuantityKg == 0)) &&
            (!trials.Any(t => t.IsActive && t.FormulaId == f.FormulaId && t.SentDate != null &&
                t.DeliveredSampleQuantityKg > 0 && t.SampleRequest.CompanyId == request.CompanyId) ||
                f.Status == "Approved" && retryFormulaId != null && f.FormulaId == retryFormulaId));

    internal static IQueryable<SampleRequestSampleTrial> Representatives(IQueryable<SampleRequestSampleTrial> latestTrials,
        IQueryable<SampleRequestSampleTrial> activeTrials, IQueryable<SampleRequestWorkflowEvent> events)
    {
        var representativeIds = from latest in latestTrials
           let batchId = events.Where(e => e.CompanyId == latest.SampleRequest.CompanyId && e.SampleRequestId == latest.SampleRequestId &&
               e.SampleRequestSampleTrialId == latest.SampleRequestSampleTrialId && e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent &&
               e.RelatedEventId != null && events.Any(b => b.Id == e.RelatedEventId && b.EventType == BatchSent))
               .Select(e => e.RelatedEventId).FirstOrDefault()
           let winner = activeTrials.Where(t => t.SampleRequestId == latest.SampleRequestId && batchId != null &&
               events.Any(e => e.CompanyId == latest.SampleRequest.CompanyId && e.SampleRequestId == latest.SampleRequestId &&
                   e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent && e.RelatedEventId == batchId && e.SampleRequestSampleTrialId == t.SampleRequestSampleTrialId))
               .OrderByDescending(t => latest.SampleRequest.Status == "Completed" && t.FormulaId == latest.SampleRequest.FormulaId)
               .ThenByDescending(t => t.Status == SampleTrialStatus.Approved)
               .ThenByDescending(t => t.Status == SampleTrialStatus.SampleSent || t.Status == SampleTrialStatus.WaitingCustomerFeedback || t.Status == SampleTrialStatus.PriceQuote)
               .ThenByDescending(t => t.TrialNo).Select(t => (Guid?)t.SampleRequestSampleTrialId).FirstOrDefault()
           select winner ?? latest.SampleRequestSampleTrialId;
        return activeTrials.Where(t => representativeIds.Contains(t.SampleRequestSampleTrialId));
    }
    internal const string BatchSent = "SampleOptionsBatchSent";
    internal static bool HasCurrentFeedback(IReadOnlyList<SampleRequestSampleTrial> trials, IReadOnlyList<SampleRequestWorkflowEvent> events)
    {
        var latest = trials.OrderByDescending(t => t.TrialNo).FirstOrDefault();
        if (latest is null) return false;
        var batchId = events.FirstOrDefault(e => e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent &&
            e.SampleRequestSampleTrialId == latest.SampleRequestSampleTrialId)?.RelatedEventId;
        if (batchId is null || !events.Any(e => e.Id == batchId && e.EventType == BatchSent)) return latest.CustomerReplyDate.HasValue;
        var ids = events.Where(e => e.RelatedEventId == batchId && e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent)
            .Select(e => e.SampleRequestSampleTrialId).ToHashSet();
        return trials.Any(t => ids.Contains(t.SampleRequestSampleTrialId) && t.CustomerReplyDate.HasValue);
    }
    internal const string OptionFeedback = "SampleOptionFeedbackRecorded";
    internal const string OptionNotSelected = "SampleOptionNotSelected";
    internal const string OptionFeedbackCorrection = "SampleOptionFeedbackCorrected";
    internal static IReadOnlyList<SampleRequestSampleTrial> FeedbackTargets(
        IReadOnlyList<SampleRequestSampleTrial> trials, SampleRequestSampleTrial selected, bool approved, bool preserveSiblings = false)
        => approved && !preserveSiblings ? trials : [selected];
    // TrialNo giữ nguyên; đại diện là option đạt của đợt mới nhất.
    internal static SampleRequestSampleTrial? Representative(IReadOnlyList<SampleRequestSampleTrial> trials,
        IReadOnlyList<SampleRequestWorkflowEvent> events, SampleRequest? request = null)
    {
        var latest = trials.OrderByDescending(t => t.TrialNo).FirstOrDefault();
        if (latest is null) return null;
        var batchId = events.FirstOrDefault(e => e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent &&
            e.SampleRequestSampleTrialId == latest.SampleRequestSampleTrialId)?.RelatedEventId;
        if (batchId is null || !events.Any(e => e.Id == batchId && e.EventType == BatchSent)) return latest;
        var ids = events.Where(e => e.RelatedEventId == batchId && e.EventType == Rules.SampleRequestWorkflowEvents.SampleSent)
            .Select(e => e.SampleRequestSampleTrialId).ToHashSet();
        return trials.Where(t => ids.Contains(t.SampleRequestSampleTrialId))
            .OrderByDescending(t => request?.Status == "Completed" && t.FormulaId == request.FormulaId)
            .ThenByDescending(t => t.Status == SampleTrialStatus.Approved)
            .ThenByDescending(t => Services.Workflow.SampleFlowRules.IsPending(t.Status))
            .ThenByDescending(t => t.TrialNo).FirstOrDefault() ?? latest;
    }
    internal static void ApplyFeedback(IReadOnlyList<SampleRequestSampleTrial> trials, SampleRequestSampleTrial selected,
        bool approved, DateTime date, string? note, Guid employee, DateTime now, bool preserveSiblings = false)
    {
        var optionNo = trials.OrderBy(t => t.TrialNo).ToList().FindIndex(t => t.SampleRequestSampleTrialId == selected.SampleRequestSampleTrialId) + 1;
        foreach (var trial in FeedbackTargets(trials, selected, approved, preserveSiblings))
        {
            var chosen = trial.SampleRequestSampleTrialId == selected.SampleRequestSampleTrialId;
            trial.Status = chosen && approved ? SampleTrialStatus.Approved : SampleTrialStatus.Failed;
            trial.CustomerReplyStatus = chosen && approved ? "APPROVED" : "FAIL";
            trial.CustomerReplyDate = date;
            trial.CustomerReplyNote = chosen ? note : string.IsNullOrWhiteSpace(trial.CustomerReplyNote)
                ? $"Không đạt do khách chọn Option {optionNo}" : trial.CustomerReplyNote;
            trial.CustomerReplyByEmployeeId = employee;
            trial.UpdatedBy = employee;
            trial.UpdatedDate = now;
        }
    }
}
