using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

internal static class SampleFlowStateReader
{
    public static async Task AttachAsync(IPLMReadDbContext db,
        IReadOnlyCollection<Dtos.SampleTrials.SampleRequestSampleTrialReportDto> items, Guid company, Guid? employee,
        bool lab, bool sale, CancellationToken ct, IReadOnlyDictionary<Guid, Guid?>? approvedSources = null,
        IReadOnlySet<Guid>? authorizedSaleRequestIds = null)
    {
        var ids = items.Select(x => x.SampleRequestId).Distinct().ToArray();
        if (ids.Length == 0) return;
        var requests = await db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == company && ids.Contains(x.SampleRequestId) && x.IsActive)
            .ToDictionaryAsync(x => x.SampleRequestId, ct);
        var trials = await db.SampleRequestSampleTrials.AsNoTracking().Where(x => ids.Contains(x.SampleRequestId) &&
                x.SampleRequest.CompanyId == company && x.IsActive).OrderByDescending(x => x.TrialNo).ToListAsync(ct);
        var events = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company && ids.Contains(x.SampleRequestId))
            .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
        var formulaIds = trials.Where(x => x.FormulaId.HasValue).Select(x => x.FormulaId!.Value)
            .Concat(events.Where(x => x.FormulaId.HasValue).Select(x => x.FormulaId!.Value))
            .Concat(requests.Values.Where(x => x.FormulaId.HasValue).Select(x => x.FormulaId!.Value)).Distinct().ToArray();
        var formulas = await db.Formulas.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && formulaIds.Contains(x.FormulaId))
            .ToDictionaryAsync(x => x.FormulaId, ct);
        var sentKgFormulaIds = (await db.SampleRequestSampleTrials.AsNoTracking().Where(x =>
            x.IsActive && x.FormulaId.HasValue && formulaIds.Contains(x.FormulaId.Value) &&
            x.SampleRequest.CompanyId == company && x.DeliveredSampleQuantityKg > 0 && x.SentDate != null)
            .Select(x => x.FormulaId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
        var trialGroups = trials.GroupBy(x => x.SampleRequestId).ToDictionary(x => x.Key, x => x.ToArray());
        var eventGroups = events.GroupBy(x => x.SampleRequestId).ToDictionary(x => x.Key, x => (IReadOnlyList<SampleRequestWorkflowEvent>)x.ToArray());
        var canRecordSharedNote = sale && employee.HasValue && await db.Employees.AsNoTracking()
            .AnyAsync(x => x.CompanyId == company && x.EmployeeId == employee && x.IsActive, ct);
        foreach (var item in items)
        {
            if (!requests.TryGetValue(item.SampleRequestId, out var request)) continue;
            var saleForRequest = sale && (authorizedSaleRequestIds?.Contains(request.SampleRequestId)
                ?? request.ManagerBy == employee);
            var requestTrials = trialGroups.GetValueOrDefault(item.SampleRequestId) ?? [];
            var latest = requestTrials.FirstOrDefault();
            var history = eventGroups.GetValueOrDefault(item.SampleRequestId) ?? [];
            item.FeedbackSummaries = BuildFeedbackSummaries(requestTrials, history);
            var latestPriceEvent = history.FirstOrDefault(x => SampleRequestPriceFeedbackVisibility.IsPriceEvent(x.EventType));
            item.LatestPriceEventId = saleForRequest ? latestPriceEvent?.Id : null;
            item.CanRecordCustomerFeedbackNote = canRecordSharedNote && saleForRequest && request.Status != "Cancelled" && latestPriceEvent != null;
            var formulaId = ResolveFormulaId(request, latest,
                history.FirstOrDefault(x => x.EventType == SampleRequestWorkflowEvents.FormulaConfirmed &&
                    x.FormulaId is { } confirmed && formulas.TryGetValue(confirmed, out var valid) && valid.ProductId == request.ProductId)?.FormulaId);
            var formula = formulaId.HasValue ? formulas.GetValueOrDefault(formulaId.Value) : null;
            if (formula?.ProductId != request.ProductId) formula = null;
            item.Flow = Build(request, latest, formula, history, lab, saleForRequest,
                formula != null && approvedSources != null && approvedSources.TryGetValue(request.ProductId, out var approved) && approved == formula.FormulaId,
                item.LabFormulaConfirmedAt.HasValue, formula != null && sentKgFormulaIds.Contains(formula.FormulaId));
            if (item.WorkDecision is { } canonical) item.Flow = item.Flow with { Stage = canonical.Stage };
            if (item.Flow.Stage == "AwaitingPriceFeedback" && !ActivePrice(history, formula?.FormulaId) &&
                (formula is null || approvedSources is null || !approvedSources.TryGetValue(request.ProductId, out var source) || source != formula.FormulaId))
                item.Flow = item.Flow with { Stage = "AwaitingStandardPrice", AllowedActions = [] };
            if (saleForRequest && item.CanConfirmCustomerPrice && item.Flow.WorkflowMode == "ChipAndSample")
                item.Flow = item.Flow with { AllowedActions = item.Flow.AllowedActions.Concat(["PriceAccepted"]).ToArray() };
        }
    }

    public static async Task<SampleFlowStateDto> ReadAsync(IPLMDbContext db, ICRMReadDbContext crm, SampleRequest request,
        bool lab, bool sale, CancellationToken ct, Guid? requestedFormulaId = null)
    {
        var events = await db.SampleRequestWorkflowEvents.AsNoTracking()
            .Where(x => x.CompanyId == request.CompanyId && x.SampleRequestId == request.SampleRequestId)
            .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
        var trial = await db.SampleRequestSampleTrials.AsNoTracking()
            .Where(x => x.SampleRequestId == request.SampleRequestId && x.IsActive)
            .OrderByDescending(x => x.TrialNo).FirstOrDefaultAsync(ct);
        // Reuse the report's exact confirmation evidence, including scoped legacy fallbacks.
        var evidence = await SampleRequestLabWorkQuery.Enrich(db.SampleRequests.AsNoTracking()
            .Where(x => x.SampleRequestId == request.SampleRequestId && x.CompanyId == request.CompanyId && x.IsActive)
            .Select(x => new SampleRequestSampleTrialReportRow
            {
                SampleRequest = x,
                Trial = db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == x.SampleRequestId && t.IsActive)
                    .OrderByDescending(t => t.TrialNo).FirstOrDefault()
            }), db).Select(x => new { x.ConfirmedFormulaId, x.LabFormulaConfirmedAt }).SingleAsync(ct);
        var formulaId = requestedFormulaId ?? ResolveFormulaId(request, trial, evidence.ConfirmedFormulaId);
        var formula = formulaId is { } id ? await db.Formulas.AsNoTracking().SingleOrDefaultAsync(x =>
            x.FormulaId == id && x.CompanyId == request.CompanyId && x.ProductId == request.ProductId && x.IsActive, ct) : null;
        var approvedSource = await crm.ProductPricingVersions.AsNoTracking()
            .Where(x => x.CompanyId == request.CompanyId && x.ProductId == request.ProductId && x.IsActive &&
                x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0 &&
                x.Currency == GetSampleRequestSampleTrialsQuery.DefaultCurrency)
            .OrderByDescending(x => x.Version).ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .Select(x => x.SourceFormulaId).FirstOrDefaultAsync(ct);
        var hasSentKg = formula != null && await db.SampleRequestSampleTrials.AsNoTracking().AnyAsync(x =>
            x.IsActive && x.FormulaId == formula.FormulaId && x.SampleRequest.CompanyId == request.CompanyId &&
            x.DeliveredSampleQuantityKg > 0 && x.SentDate != null, ct);
        var state = Build(request, trial, formula, events, lab, sale,
            formula != null && approvedSource == formula.FormulaId, evidence.LabFormulaConfirmedAt.HasValue, hasSentKg);
        return state;
    }

    public static SampleFlowStateDto Build(SampleRequest request, SampleRequestSampleTrial? trial, Formula? formula,
        IReadOnlyList<SampleRequestWorkflowEvent> events, bool lab, bool sale,
        bool hasApprovedPrice = false, bool labConfirmed = false, bool hasSentKg = false)
    {
        var legacy = SampleFlowRules.IsLegacy(request.CreatedDate);
        var actions = new List<string>();
        var lastChipDecision = events.FirstOrDefault(x => x.EventType is SampleFlowRules.ChipFinalized or SampleFlowRules.ChipReopened &&
            x.FormulaId == formula?.FormulaId && x.SampleRequestSampleTrialId == trial?.SampleRequestSampleTrialId);
        var finalized = formula?.Status == "Completed" && lastChipDecision?.EventType == SampleFlowRules.ChipFinalized;
        var decision = Services.SampleWorkDecisionRules.Resolve(new Services.SampleWorkFacts
        {
            Open = SampleFlowRules.IsOpen(request.Status), Legacy = legacy, Sent = request.Status == "SampleSent",
            Quotation = SampleRequestCustomerPriceRules.IsQuotation(request.InfoType), RequestedKg = request.SampleQuantity > 0,
            HasSentKg = hasSentKg, HasTrial = trial != null, PendingTrial = trial != null && SampleFlowRules.IsPending(trial.Status),
            Chip = trial?.DeliveryKind == SampleDeliveryKind.ColorChip, Kg = trial?.DeliveryKind == SampleDeliveryKind.TrialSample,
            Failed = trial?.Status == SampleTrialStatus.Failed, Approved = trial?.Status == SampleTrialStatus.Approved,
            UnknownSent = trial?.DeliveryKind == SampleDeliveryKind.Unknown && trial.SentDate.HasValue,
            FormulaStatus = formula?.Status, FinalizedChip = finalized, AcceptedPrice = ActivePrice(events, formula?.FormulaId),
            ApprovedPrice = hasApprovedPrice, LabConfirmed = labConfirmed,
            Quoted = request.RealPriceQuoteDate.HasValue
        });
        var stage = decision.Stage;
        // The main assignment remains rework until the next delivery is recorded.
        // Retrying a failed chip is an allowed delivery action on the same editable VU.
        var canResendFailedChip = decision.Code == "ReworkFormula" && !legacy &&
            request.SampleQuantity is not > 0 && trial?.DeliveryKind == SampleDeliveryKind.ColorChip &&
            trial.Status == SampleTrialStatus.Failed && trial.FormulaId == formula?.FormulaId &&
            formula?.Status is "Rework" or "Approved";
        // A draft round is not a prior delivery; the first send can still select its candidate VU.
        var matchesDelivery = trial?.SentDate == null || trial.FormulaId == formula?.FormulaId;
        if (lab && matchesDelivery && (decision.Code == "SendSample" || canResendFailedChip)) actions.Add("Send");
        if (lab && decision.Code == "FinalizeQuotation") actions.Add("FinalizeQuotation");
        if (lab && decision.Code == "FinalizeChip") actions.Add("FinalizeChip");
        if (lab && finalized && !hasSentKg && SampleFlowRules.IsOpen(request.Status)) actions.Add("ReopenChip");
        if (sale && decision.Code == "CompleteRequest" && (!legacy || formula?.Status == "SampleSent")) actions.Add("Complete");
        if (sale && stage == "AwaitingSaleDecision") actions.Add("RequestSample");
        if (sale && decision.Code == "PriceFeedback" && request.RealPriceQuoteDate.HasValue)
            actions.AddRange(["PriceRejected", "PriceNoResponse"]);
        return new(legacy ? "Legacy" : "ChipAndSample", stage, formula?.FormulaId,
            request.UpdatedDate, formula is null ? null : formula.UpdatedDate ?? formula.CreatedDate,
            request.SampleQuantity, actions);
    }

    internal static SampleFeedbackSummaryDto[] BuildFeedbackSummaries(
        IReadOnlyList<SampleRequestSampleTrial> trials, IReadOnlyList<SampleRequestWorkflowEvent> events)
    {
        var summaries = new List<SampleFeedbackSummaryDto>();
        var actualPrice = events.Where(x => x.EventType is SampleRequestWorkflowEvents.PriceAccepted or
            SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse)
            .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).FirstOrDefault();
        if (actualPrice != null)
        {
            var latestPrice = events.Where(x => x.EventType is SampleRequestWorkflowEvents.PriceAccepted or
                SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse or SampleRequestWorkflowEvents.PriceRevoked)
                .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).First();
            var outcome = latestPrice.EventType switch
            {
                SampleRequestWorkflowEvents.PriceAccepted => "Accepted",
                SampleFlowRules.PriceRejected => "Rejected",
                SampleFlowRules.PriceNoResponse => "NoResponse",
                _ => "Revoked"
            };
            var shared = events.Where(x => x.EventType == SampleFlowRules.PriceFeedbackShared && x.RelatedEventId == latestPrice.Id &&
                x.CompanyId == latestPrice.CompanyId && x.SampleRequestId == latestPrice.SampleRequestId)
                .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).FirstOrDefault();
            summaries.Add(new("Price", outcome, latestPrice.OccurredAt ?? latestPrice.RecordedAt,
                FeedbackNote(shared?.Note), IsNoteHidden: shared is null) { IsSharedNote = shared != null });
        }
        foreach (var group in trials.Where(x => x.CustomerReplyDate.HasValue).GroupBy(x => x.DeliveryKind))
        {
            var trial = group.OrderByDescending(x => x.TrialNo).ThenByDescending(x => x.CustomerReplyDate).First();
            var kind = group.Key switch
            {
                SampleDeliveryKind.ColorChip => "ColorChip", SampleDeliveryKind.TrialSample => "TrialSample", _ => "UnknownDelivery"
            };
            var outcome = trial.Status switch
            {
                SampleTrialStatus.Approved => "Accepted", SampleTrialStatus.Failed => "Rejected",
                SampleTrialStatus.Cancelled => "Cancelled",
                SampleTrialStatus.WaitingCustomerFeedback or SampleTrialStatus.PriceQuote or SampleTrialStatus.SampleSent => "NoResponse",
                _ => "Recorded"
            };
            summaries.Add(new(kind, outcome, trial.CustomerReplyDate!.Value, FeedbackNote(trial.CustomerReplyNote)));
        }
        return summaries.OrderBy(x => x.Kind).ToArray();
    }

    private static string? FeedbackNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    internal static Guid? ResolveFormulaId(SampleRequest request, SampleRequestSampleTrial? trial, Guid? confirmedFormulaId)
        => (SampleFlowRules.IsLegacy(request.CreatedDate) ? request.FormulaId : trial?.FormulaId) ?? confirmedFormulaId ?? request.FormulaId;

    internal static bool ActivePrice(IReadOnlyList<SampleRequestWorkflowEvent> events, Guid? formulaId)
    {
        var latest = events.FirstOrDefault(x => x.EventType is SampleRequestWorkflowEvents.PriceAccepted or
            SampleRequestWorkflowEvents.PriceRevoked or SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse);
        return latest?.EventType == SampleRequestWorkflowEvents.PriceAccepted && formulaId.HasValue && latest.FormulaId == formulaId;
    }
}
