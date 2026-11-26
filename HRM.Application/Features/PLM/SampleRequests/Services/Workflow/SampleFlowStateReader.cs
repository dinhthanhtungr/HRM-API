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
        IReadOnlySet<Guid>? authorizedSaleRequestIds = null, IReadOnlyDictionary<Guid, DateTime?>? approvedPriceTimes = null)
    {
        var ids = items.Select(x => x.SampleRequestId).Distinct().ToArray();
        if (ids.Length == 0) return;
        var requests = await db.SampleRequests.AsNoTracking().Include(x => x.Customer).Where(x => x.CompanyId == company && ids.Contains(x.SampleRequestId) && x.IsActive)
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
        var unlinkedIds = requests.Values.Where(r => r.FormulaId == null &&
            !trials.Any(t => t.SampleRequestId == r.SampleRequestId && t.FormulaId.HasValue) &&
            !events.Any(e => e.SampleRequestId == r.SampleRequestId && e.EventType == SampleRequestWorkflowEvents.FormulaConfirmed && e.FormulaId.HasValue))
            .Select(r => r.SampleRequestId).ToArray();
        var candidateRequests = unlinkedIds.Length == 0 ? new HashSet<Guid>() : (await DeliveryOptions.SampleDeliveryRules.RequestsWithUnsentConfirmedFormula(
                db.SampleRequests.Where(x => x.CompanyId == company && unlinkedIds.Contains(x.SampleRequestId)), db.Formulas, db.SampleRequestSampleTrials)
            .ToArrayAsync(ct)).ToHashSet();
        var trialGroups = trials.GroupBy(x => x.SampleRequestId).ToDictionary(x => x.Key, x => x.ToArray());
        var eventGroups = events.GroupBy(x => x.SampleRequestId).ToDictionary(x => x.Key, x => (IReadOnlyList<SampleRequestWorkflowEvent>)x.ToArray());
        var canRecordSharedNote = sale && employee.HasValue && await db.Employees.AsNoTracking()
            .AnyAsync(x => x.CompanyId == company && x.EmployeeId == employee && x.IsActive, ct);
        var eventActors = events.Where(x => x.RecordedByEmployeeId.HasValue).Select(x => x.RecordedByEmployeeId!.Value).Distinct().ToArray();
        var actorNames = await db.Employees.AsNoTracking().Where(x => x.CompanyId == company && eventActors.Contains(x.EmployeeId))
            .Select(x => new { x.EmployeeId, x.FullName }).ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, ct);
        foreach (var item in items)
        {
            if (!requests.TryGetValue(item.SampleRequestId, out var request)) continue;
            var saleForRequest = sale && (authorizedSaleRequestIds?.Contains(request.SampleRequestId)
                ?? request.ManagerBy == employee);
            var requestTrials = trialGroups.GetValueOrDefault(item.SampleRequestId) ?? [];
            var history = eventGroups.GetValueOrDefault(item.SampleRequestId) ?? [];
            var latest = DeliveryOptions.SampleDeliveryRules.Representative(requestTrials, history, request);
            item.HasCurrentDeliveryFeedback = DeliveryOptions.SampleDeliveryRules.HasCurrentFeedback(requestTrials, history);
            item.DeliveryBatchId = history.FirstOrDefault(e => e.EventType == SampleRequestWorkflowEvents.SampleSent &&
                e.SampleRequestSampleTrialId == item.SampleRequestSampleTrialId && e.RelatedEventId != null &&
                history.Any(b => b.Id == e.RelatedEventId && b.EventType == DeliveryOptions.SampleDeliveryRules.BatchSent))?.RelatedEventId;
            if (saleForRequest && SampleFlowRules.IsOpen(request.Status) && latest != null && history.Any(e =>
                e.EventType == SampleRequestWorkflowEvents.SampleSent && e.SampleRequestSampleTrialId == latest.SampleRequestSampleTrialId &&
                e.RelatedEventId != null && history.Any(b => b.Id == e.RelatedEventId && b.EventType == DeliveryOptions.SampleDeliveryRules.BatchSent)))
                item.CanUpdateCustomerFeedback = true;
            item.FeedbackSummaries = BuildFeedbackSummaries(requestTrials, history);
            foreach (var summary in item.FeedbackSummaries)
                summary.RecordedByName = summary.RecordedByEmployeeId is { } summaryActor ? actorNames.GetValueOrDefault(summaryActor) : null;
            var latestPriceEvent = history.FirstOrDefault(x => SampleRequestPriceFeedbackVisibility.IsPriceEvent(x.EventType));
            item.LatestPriceEventId = saleForRequest ? latestPriceEvent?.Id : null;
            item.CanRecordCustomerFeedbackNote = canRecordSharedNote && saleForRequest && request.Status != "Cancelled" && request.Status != "LabDeclined" && latestPriceEvent != null;
            var formulaId = ResolveFormulaId(request, latest,
                history.FirstOrDefault(x => x.EventType == SampleRequestWorkflowEvents.FormulaConfirmed &&
                    x.FormulaId is { } confirmed && formulas.TryGetValue(confirmed, out var valid) && valid.ProductId == request.ProductId)?.FormulaId, history);
            var formula = formulaId.HasValue ? formulas.GetValueOrDefault(formulaId.Value) : null;
            if (formula?.ProductId != request.ProductId) formula = null;
            if (item.WorkDecision is null)
            {
                item.WorkDecision = ResolveDecision(request, latest, formula, history,
                    formula != null && approvedSources != null && approvedSources.TryGetValue(request.ProductId, out var decisionSource) && decisionSource.HasValue,
                    item.LabFormulaConfirmedAt.HasValue, formula != null && sentKgFormulaIds.Contains(formula.FormulaId),
                    approvedPriceTimes?.GetValueOrDefault(request.ProductId), requestTrials, formula == null && candidateRequests.Contains(request.SampleRequestId));
                item.NeedsLabSchedule = SampleFlowRules.IsOpen(request.Status) && (item.WorkDecision.Role is "Lab" or "LabAdmin") &&
                    (SampleRequestCustomerPriceRules.IsQuotation(request.InfoType) && request.SampleQuantity is not > 0
                        ? request.ExpectedPriceQuoteDate == null : request.ExpectedDeliveryDate == null);
            }
            item.Flow = Build(request, latest, formula, history, lab, saleForRequest,
                formula != null && approvedSources != null && approvedSources.TryGetValue(request.ProductId, out var approved) && approved.HasValue,
                item.LabFormulaConfirmedAt.HasValue, formula != null && sentKgFormulaIds.Contains(formula.FormulaId),
                approvedPriceTimes?.GetValueOrDefault(request.ProductId), requestTrials, formula == null && candidateRequests.Contains(request.SampleRequestId));
            if (item.Flow.IsLabPaused)
            {
                // Resume follows readable TP scope, not Sale ownership of other workflow actions.
                item.Flow = item.Flow with { AllowedActions = sale ? ["ResumeAfterLabRejection"] : [] };
                item.CanUpdateCustomerFeedback = false;
                item.CanConfirmCustomerPrice = false;
                item.CanRecordCustomerFeedbackNote = false;
                continue;
            }
            if (ChipThenSampleRules.HasBlockingPending(requestTrials, events: history))
                item.Flow = item.Flow with { PlanActions = item.Flow.PlanActions.Where(x => x is not ("RequestChip" or ChipThenSampleRules.Action)).ToArray() };
            if (item.WorkDecision is { } canonical) item.Flow = item.Flow with { Stage = canonical.Stage };
            if (item.Flow.Stage == "AwaitingPriceFeedback" && !ActivePrice(history, formula?.FormulaId) &&
                (formula is null || approvedSources is null || !approvedSources.TryGetValue(request.ProductId, out var source) || !source.HasValue))
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
        var requestTrials = await db.SampleRequestSampleTrials.AsNoTracking()
            .Where(x => x.SampleRequestId == request.SampleRequestId && x.IsActive)
            .OrderByDescending(x => x.TrialNo).ToListAsync(ct);
        var trial = DeliveryOptions.SampleDeliveryRules.Representative(requestTrials, events, request);
        if (requestedFormulaId.HasValue && trial is not null)
        {
            var batchId = events.FirstOrDefault(e => e.EventType == SampleRequestWorkflowEvents.SampleSent &&
                e.SampleRequestSampleTrialId == trial.SampleRequestSampleTrialId)?.RelatedEventId;
            var selected = requestTrials.FirstOrDefault(t => t.FormulaId == requestedFormulaId &&
                t.Status == SampleTrialStatus.Approved && batchId.HasValue && events.Any(e =>
                    e.EventType == SampleRequestWorkflowEvents.SampleSent && e.RelatedEventId == batchId &&
                    e.SampleRequestSampleTrialId == t.SampleRequestSampleTrialId));
            if (selected is not null) trial = selected;
        }
        // Reuse the report's exact confirmation evidence, including scoped legacy fallbacks.
        var evidence = await SampleRequestLabWorkQuery.Enrich(db.SampleRequests.AsNoTracking()
            .Where(x => x.SampleRequestId == request.SampleRequestId && x.CompanyId == request.CompanyId && x.IsActive)
            .Select(x => new SampleRequestSampleTrialReportRow
            {
                SampleRequest = x,
                Trial = db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == x.SampleRequestId && t.IsActive)
                    .OrderByDescending(t => t.TrialNo).FirstOrDefault()
            }), db).Select(x => new { x.ConfirmedFormulaId, x.LabFormulaConfirmedAt }).SingleAsync(ct);
        var formulaId = requestedFormulaId ?? ResolveFormulaId(request, trial, evidence.ConfirmedFormulaId, events);
        var formula = formulaId is { } id ? await db.Formulas.AsNoTracking().SingleOrDefaultAsync(x =>
            x.FormulaId == id && x.CompanyId == request.CompanyId && x.ProductId == request.ProductId && x.IsActive, ct) : null;
        var approvedSource = await crm.ProductPricingVersions.AsNoTracking()
            .Where(x => x.CompanyId == request.CompanyId && x.ProductId == request.ProductId && x.IsActive &&
                x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0 &&
                x.Currency == GetSampleRequestSampleTrialsQuery.DefaultCurrency)
            .OrderByDescending(x => x.Version).ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new { x.SourceFormulaId, At = (DateTime?)(x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate) }).FirstOrDefaultAsync(ct);
        var hasSentKg = formula != null && await db.SampleRequestSampleTrials.AsNoTracking().AnyAsync(x =>
            x.IsActive && x.FormulaId == formula.FormulaId && x.SampleRequest.CompanyId == request.CompanyId &&
            x.DeliveredSampleQuantityKg > 0 && x.SentDate != null, ct);
        var hasCandidate = formula == null && await DeliveryOptions.SampleDeliveryRules.RequestsWithUnsentConfirmedFormula(
                db.SampleRequests.Where(x => x.CompanyId == request.CompanyId && x.SampleRequestId == request.SampleRequestId), db.Formulas, db.SampleRequestSampleTrials).AnyAsync(ct);
        var customerCode = request.Customer?.ExternalId ?? await db.Customers.AsNoTracking()
            .Where(x => x.CustomerId == request.CustomerId && x.CompanyId == request.CompanyId)
            .Select(x => x.ExternalId).SingleOrDefaultAsync(ct);
        var internalDirect = HRM.Application.Features.PLM.Shared.Rules.PLMInternalSampleRules.CanCompleteDirectly(request.RequestType, customerCode);
        var state = Build(request, trial, formula, events, lab, sale,
            formula != null && approvedSource?.SourceFormulaId is not null, evidence.LabFormulaConfirmedAt.HasValue, hasSentKg, approvedSource?.At, requestTrials, hasCandidate, internalDirect);
        if (state.PlanActions.Any(x => x is "RequestChip" or "RequestSample" or ChipThenSampleRules.Action) &&
            await ChipThenSampleRules.BlockingPending(db.SampleRequestSampleTrials.AsNoTracking())
                .AnyAsync(x => x.SampleRequestId == request.SampleRequestId, ct))
            state = state with { PlanActions = state.PlanActions.Where(x => x is not ("RequestChip" or ChipThenSampleRules.Action)).ToArray() };
        return state;
    }

    public static SampleFlowStateDto Build(SampleRequest request, SampleRequestSampleTrial? trial, Formula? formula,
        IReadOnlyList<SampleRequestWorkflowEvent> events, bool lab, bool sale,
        bool hasApprovedPrice = false, bool labConfirmed = false, bool hasSentKg = false, DateTime? approvedPriceAt = null,
        IReadOnlyCollection<SampleRequestSampleTrial>? trials = null, bool hasUnlinkedDeliveryCandidate = false, bool internalDirect = false)
    {
        var legacy = SampleFollowUpRules.IsLegacy(request, events);
        if (request.Status == "LabDeclined")
            return new(legacy ? "Legacy" : "ChipAndSample", "LabDeclined", formula?.FormulaId, request.UpdatedDate,
                formula is null ? null : formula.UpdatedDate ?? formula.CreatedDate, request.SampleQuantity, [])
            {
                LabRejectionReason = events.FirstOrDefault(e => e.EventType == SampleRequestWorkflowEvents.LabDeclined)?.Note,
                PlanActions = SampleRequestPlanRules.Actions(request, trial, formula, sale, labConfirmed),
                CompletionBlockers = ["RequestClosed"], CompletionVisible = false
            };
        if (SampleFlowRules.IsOpen(request.Status) && SampleRequestLabPauseRules.IsPaused(events))
            return new(legacy ? "Legacy" : "ChipAndSample", "LabRejected", formula?.FormulaId, request.UpdatedDate,
                formula is null ? null : formula.UpdatedDate ?? formula.CreatedDate, request.SampleQuantity,
                sale ? ["ResumeAfterLabRejection"] : [])
            { IsLabPaused = true, LabRejectionReason = SampleRequestLabPauseRules.Latest(events)?.Note, CompletionBlockers = ["LabPaused"], CompletionVisible = false };
        var directInternal = internalDirect || request.Customer?.CompanyId == request.CompanyId && HRM.Application.Features.PLM.Shared.Rules.PLMInternalSampleRules.CanCompleteDirectly(request.RequestType, request.Customer?.ExternalId);
        if (directInternal)
            return new("InternalDirect", SampleFlowRules.IsOpen(request.Status) ? "InternalFormulaCompletion" : "Closed", formula?.FormulaId,
                request.UpdatedDate, formula is null ? null : formula.UpdatedDate ?? formula.CreatedDate, request.SampleQuantity, [])
                { CompletionVisible = false, CompletionBlockers = ["InternalCompletionOnly"] };
        var actions = new List<string>();
        var lastChipDecision = events.FirstOrDefault(x => x.EventType is SampleFlowRules.ChipFinalized or SampleFlowRules.ChipReopened &&
            x.FormulaId == formula?.FormulaId && x.SampleRequestSampleTrialId == trial?.SampleRequestSampleTrialId);
        var finalized = formula?.Status == "Completed" && lastChipDecision?.EventType == SampleFlowRules.ChipFinalized;
        var chipToSample = ChipThenSampleRules.CanContinueWithSample(request, trial, events, trials ?? (trial is null ? [] : [trial]));
        var decision = ResolveDecision(request, trial, formula, events, hasApprovedPrice, labConfirmed, hasSentKg, approvedPriceAt, trials, hasUnlinkedDeliveryCandidate);
        var stage = decision.Stage;
        var canChangeCompletion = !legacy && SampleFlowRules.IsOpen(request.Status) && !hasSentKg &&
            formula != null && (trial is null || !SampleFlowRules.IsPending(trial.Status)) &&
            stage is "FinalizeChipFormula" or "FinalizeQuotationFormula" or "AwaitingSaleDecision" or "QuotationAccepted" or "RequestFollowUp";
        if (sale && canChangeCompletion)
            actions.AddRange(["RequestChip", "RequestSample", ChipThenSampleRules.Action, "WaitRequest", "CancelRejectedRequest"]);
        if (sale && stage == "RequestFollowUp" && trial?.DeliveryKind == SampleDeliveryKind.ColorChip && trial.Status == SampleTrialStatus.Approved)
            actions.Add("RequestChipFinalization");
        if (sale && stage == "RequestFollowUp" && trial is null && ActivePrice(events, formula?.FormulaId))
            actions.Add("RequestLabFinalization");
        if (sale && stage == "RequestFollowUp" && formula?.Status == "Completed" &&
            (finalized || (trial is null && labConfirmed && ActivePrice(events, formula.FormulaId)))) actions.Add("Complete");
        if (sale && stage == "AwaitingSaleAfterPrice")
            actions.AddRange(["RequestLabFinalization", "RequestChip", "RequestSample", ChipThenSampleRules.Action, "WaitRequest", "CancelRejectedRequest"]);
        if (sale && stage is "AwaitingPriceDecision" or "PriceFollowUp")
            actions.AddRange(["RequestLowerPrice", "CancelRejectedRequest", "WaitCustomerReply"]);
        if (sale && stage is "AwaitingSaleAfterChip" or "ChipFollowUp")
            actions.AddRange(["RequestSample", finalized ? "Complete" : "RequestChipFinalization", "WaitChipReply", "CancelRejectedRequest"]);
        // The main assignment remains rework until the next delivery is recorded.
        // Retrying a failed chip is an allowed delivery action on the same editable VU.
        var canResendFailedChip = decision.Code == "ReworkFormula" && !legacy &&
            request.SampleQuantity is not > 0 && trial?.DeliveryKind == SampleDeliveryKind.ColorChip &&
            trial.Status == SampleTrialStatus.Failed && trial.FormulaId == formula?.FormulaId &&
            formula?.Status is "Rework" or "Approved";
        var matchesDelivery = trial?.SentDate == null || trial.Status == SampleTrialStatus.Failed ||
            chipToSample || SampleRequestPlanRules.HasOutstandingDelivery(events) || trial.FormulaId == formula?.FormulaId;
        if (lab && matchesDelivery && (decision.Code == "SendSample" || canResendFailedChip)) actions.Add("Send");
        // The detail picker may choose another confirmed VU. The send endpoint resolves
        // that exact VU again and still rejects drafts, pending trials and sent kg VUs.
        if (lab && !legacy && SampleFlowRules.IsOpen(request.Status) &&
            (!hasSentKg || formula?.Status is "SampleSent" or "Completed" || trial?.Status == SampleTrialStatus.Failed) &&
            (formula?.Status is "Approved" or "SampleSent" ||
                formula?.Status == "Completed" && SampleRequestPlanRules.HasOutstandingDelivery(events)) &&
            !ChipThenSampleRules.HasBlockingPending(trials ?? (trial is null ? [] : [trial]), events: events) &&
            (trial?.Status == SampleTrialStatus.Failed || SampleRequestPlanRules.HasOutstandingDelivery(events)))
            actions.Add("Send");
        if (lab && decision.Code == "FinalizeQuotation") actions.Add("FinalizeQuotation");
        if (lab && !legacy && SampleFlowRules.IsOpen(request.Status)) actions.Add("RejectFormula");
        if (lab && decision.Code == "FinalizeChip") actions.Add("FinalizeChip");
        if (lab && finalized && !hasSentKg && SampleFlowRules.IsOpen(request.Status)) actions.Add("ReopenChip");
        if (sale && decision.Code == "CompleteRequest" && (!legacy || formula?.Status == "SampleSent")) actions.Add("Complete");
        if (sale && !legacy && SampleFlowRules.IsOpen(request.Status) && trial?.DeliveryKind == SampleDeliveryKind.TrialSample && trial.SentDate.HasValue)
            actions.Add("CancelRejectedRequest");
        // Quotation feedback is parallel to chip/kg preparation. The combined command
        // records the quotation date before applying feedback; direct actions still require it.
        var canRecordParallelPrice = !legacy && SampleFlowRules.IsOpen(request.Status) &&
            labConfirmed && hasApprovedPrice && formula?.Status is "Approved" or "SampleSent" &&
            !ActivePrice(events, formula.FormulaId) &&
            !NeedsPriceAdjustment(events, approvedPriceAt) && !NeedsRevisedFormulaPrice(events, approvedPriceAt);
        if (sale && ((decision.Code == "PriceFeedback" && request.RealPriceQuoteDate.HasValue) || canRecordParallelPrice))
            actions.AddRange(["PriceRejected", "PriceNoResponse"]);
        if (!legacy && ((KgSampleCompletionRules.RequiresPriceAcceptance(request, events) && !ActivePrice(events, formula?.FormulaId)) ||
            (request.SampleQuantity > 0 && trial?.DeliveryKind != SampleDeliveryKind.TrialSample) ||
            (trial?.DeliveryKind == SampleDeliveryKind.ColorChip && trial.Status != SampleTrialStatus.Approved)))
            actions.RemoveAll(action => action == "Complete");
        var blockers = new List<string>();
        if (!SampleFlowRules.IsOpen(request.Status)) blockers.Add("RequestClosed");
        if (!legacy)
        {
            if (KgSampleCompletionRules.RequiresPriceAcceptance(request, events) && !ActivePrice(events, formula?.FormulaId))
                blockers.Add("PriceAcceptanceRequired");
            if (request.SampleQuantity > 0 && !(trial is { SentDate: not null, Status: SampleTrialStatus.Approved,
                DeliveryKind: SampleDeliveryKind.TrialSample } && trial.FormulaId == formula?.FormulaId))
                blockers.Add("SampleAcceptanceRequired");
            if (request.SampleQuantity is not > 0 && trial?.DeliveryKind == SampleDeliveryKind.ColorChip &&
                !(trial.SentDate.HasValue && trial.Status == SampleTrialStatus.Approved && trial.FormulaId == formula?.FormulaId))
                blockers.Add("ChipAcceptanceRequired");
            if (SampleRequestPlanRules.HasPendingPlan(request, events, trials ?? (trial is null ? [] : [trial])))
                blockers.Add("PendingDelivery");
            if (formula == null || (request.SampleQuantity > 0 ? formula.Status is not ("SampleSent" or "Completed") : formula.Status != "Completed"))
                blockers.Add("FormulaFinalizationRequired");
        }
        if (blockers.Count > 0) actions.RemoveAll(action => action == "Complete");
        if (!actions.Contains("Complete") && blockers.Count == 0) blockers.Add("CompletionUnavailable");
        return new(legacy ? "Legacy" : "ChipAndSample", stage, formula?.FormulaId,
            request.UpdatedDate, formula is null ? null : formula.UpdatedDate ?? formula.CreatedDate,
            request.SampleQuantity, actions.Distinct().ToArray())
        {
            DeliveryKind = !legacy && request.SampleQuantity is not > 0 ? "ColorChip" : "TrialSample",
            ChipBeforeSample = !legacy && ChipThenSampleRules.LatestPlan(events)?.EventType == SampleFlowRules.ChipThenSampleRequested,
            PlanActions = SampleRequestPlanRules.Actions(request, trial, formula, sale, labConfirmed),
            DeliveryNeedsNewRequest = SampleRequestPlanRules.NeedsNewRequest(trial, hasSentKg),
            CompletionBlockers = blockers,
            CompletionVisible = legacy || !SampleRequestCustomerPriceRules.IsQuotation(request.InfoType) ||
                request.SampleQuantity > 0 || trial != null || formula?.Status == "Completed",
            HasPendingDeliveryPlan = SampleRequestPlanRules.HasPendingPlan(request, events, trials ?? (trial is null ? [] : [trial]))
        };
    }

    internal static Services.SampleWorkDecision ResolveDecision(SampleRequest request, SampleRequestSampleTrial? trial, Formula? formula,
        IReadOnlyList<SampleRequestWorkflowEvent> events, bool hasApprovedPrice, bool labConfirmed, bool hasSentKg, DateTime? approvedPriceAt,
        IReadOnlyCollection<SampleRequestSampleTrial>? trials = null, bool hasUnlinkedDeliveryCandidate = false, bool internalDirect = false)
    {
        var lastChipDecision = events.FirstOrDefault(x => x.EventType is SampleFlowRules.ChipFinalized or SampleFlowRules.ChipReopened &&
            x.FormulaId == formula?.FormulaId && x.SampleRequestSampleTrialId == trial?.SampleRequestSampleTrialId);
        var finalized = formula?.Status == "Completed" && lastChipDecision?.EventType == SampleFlowRules.ChipFinalized;
        var chipSaleChoice = events.FirstOrDefault(x => trial != null && x.FormulaId == formula?.FormulaId &&
            x.SampleRequestSampleTrialId == trial.SampleRequestSampleTrialId &&
            x.EventType is SampleFlowRules.ChipFinalizationRequested or SampleFlowRules.ChipFollowUpDeferred or SampleFlowRules.ChipRequested or SampleFlowRules.KgRequested or SampleFlowRules.CompletionDeferred);
        var completionChoice = events.FirstOrDefault(x => x.EventType is SampleFlowRules.ChipFinalizationRequested or
            SampleFlowRules.LabFinalizationRequested or SampleFlowRules.ChipThenSampleRequested or SampleFlowRules.ChipRequested or SampleFlowRules.KgRequested or SampleFlowRules.CompletionDeferred);
        return Services.SampleWorkDecisionRules.Resolve(new Services.SampleWorkFacts
        {
            HasUnlinkedDeliveryCandidate = hasUnlinkedDeliveryCandidate,
            InternalDirect = internalDirect || request.Customer?.CompanyId == request.CompanyId && HRM.Application.Features.PLM.Shared.Rules.PLMInternalSampleRules.CanCompleteDirectly(request.RequestType, request.Customer?.ExternalId),
            Open = SampleFlowRules.IsOpen(request.Status), Legacy = SampleFollowUpRules.IsLegacy(request, events), Sent = request.Status == "SampleSent",
            LabPaused = SampleRequestLabPauseRules.IsPaused(events),
            Quotation = SampleRequestCustomerPriceRules.IsQuotation(request.InfoType), RequestedKg = request.SampleQuantity > 0,
            ChipFirstPending = false,
            ChipToSample = ChipThenSampleRules.CanContinueWithSample(request, trial, events, trials ?? (trial is null ? [] : [trial])),
            HasSentKg = hasSentKg, HasTrial = trial != null, PendingTrial = trial != null && SampleFlowRules.IsPending(trial.Status) && !ChipThenSampleRules.IsBeforeSampleRequest(trial, events),
            Chip = trial?.DeliveryKind == SampleDeliveryKind.ColorChip, Kg = trial?.DeliveryKind == SampleDeliveryKind.TrialSample,
            Failed = trial?.Status == SampleTrialStatus.Failed, Approved = trial?.Status == SampleTrialStatus.Approved,
            UnknownSent = trial?.DeliveryKind == SampleDeliveryKind.Unknown && trial.SentDate.HasValue,
            FormulaStatus = formula?.Status, FinalizedChip = finalized, AcceptedPrice = ActivePrice(events, formula?.FormulaId),
            RequiresPriceAcceptance = KgSampleCompletionRules.RequiresPriceAcceptance(request, events),
            ChipFinalizationRequested = chipSaleChoice?.EventType == SampleFlowRules.ChipFinalizationRequested,
            ChipDecisionDeferred = chipSaleChoice?.EventType == SampleFlowRules.ChipFollowUpDeferred,
            ChipDeliveryRequested = chipSaleChoice?.EventType == SampleFlowRules.ChipRequested,
            DeliveryRequested = SampleRequestPlanRules.HasOutstandingDelivery(events),
            CompletionDeferred = completionChoice?.EventType == SampleFlowRules.CompletionDeferred && completionChoice.FormulaId == formula?.FormulaId,
            ApprovedPrice = hasApprovedPrice, LabConfirmed = labConfirmed,
            Quoted = request.RealPriceQuoteDate.HasValue,
            PriceAdjustmentRequired = NeedsPriceAdjustment(events, hasApprovedPrice ? approvedPriceAt : null),
            RevisedFormulaNeedsPrice = NeedsRevisedFormulaPrice(events, hasApprovedPrice ? approvedPriceAt : null),
            PriceDecisionRequired = NeedsPriceDecision(events, hasApprovedPrice ? approvedPriceAt : null),
            PriceDecisionDeferred = IsPriceDecisionDeferred(events),
            PriceNextStepRequired = trial?.SentDate == null && NeedsAfterPriceDecision(events, formula?.FormulaId),
            LabFinalizationRequested = trial?.SentDate == null && ActivePrice(events, formula?.FormulaId) &&
                events.FirstOrDefault(x => x.EventType is SampleFlowRules.ChipThenSampleRequested or SampleFlowRules.ChipRequested or SampleFlowRules.KgRequested or SampleFlowRules.LabFinalizationRequested or SampleFlowRules.CompletionDeferred) is { } nextStep &&
                nextStep.EventType == SampleFlowRules.LabFinalizationRequested && nextStep.RecordedAt >= events.First(x => x.EventType == SampleRequestWorkflowEvents.PriceAccepted).RecordedAt
        });
    }

    internal static bool NeedsAfterPriceDecision(IReadOnlyList<SampleRequestWorkflowEvent> events, Guid? formulaId)
    {
        if (!ActivePrice(events, formulaId)) return false;
        if (ChipThenSampleRules.LatestPlan(events)?.EventType == SampleFlowRules.ChipThenSampleRequested)
            return false;
        var accepted = events.First(x => x.EventType == SampleRequestWorkflowEvents.PriceAccepted);
        return !events.Any(x => x.RecordedAt >= accepted.RecordedAt && x.EventType is
            SampleFlowRules.ChipThenSampleRequested or SampleFlowRules.ChipRequested or SampleFlowRules.KgRequested or SampleFlowRules.LabFinalizationRequested);
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
                FeedbackNote(shared?.Note), IsNoteHidden: shared is null) { IsSharedNote = shared != null,
                    RecordedByEmployeeId = latestPrice.RecordedByEmployeeId, RecordedAt = latestPrice.RecordedAt });
        }
        foreach (var group in trials.Where(x => x.CustomerReplyDate.HasValue).GroupBy(x => x.DeliveryKind))
        {
            var trial = DeliveryOptions.SampleDeliveryRules.Representative(group.OrderByDescending(x => x.TrialNo).ToArray(), events)!;
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
            var feedbackEvent = events.Where(x => (x.EventType == SampleRequestWorkflowEvents.FeedbackRecorded ||
                x.EventType == HRM.Application.Features.PLM.SampleRequests.SampleTrials.SampleTrialResultCorrection.EventType) &&
                x.SampleRequestSampleTrialId == trial.SampleRequestSampleTrialId).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).FirstOrDefault();
            summaries.Add(new(kind, outcome, trial.CustomerReplyDate!.Value, FeedbackNote(trial.CustomerReplyNote))
                { RecordedByEmployeeId = feedbackEvent?.RecordedByEmployeeId, RecordedAt = feedbackEvent?.RecordedAt });
        }
        return summaries.OrderBy(x => x.Kind).ToArray();
    }

    private static string? FeedbackNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    internal static Guid? ResolveFormulaId(SampleRequest request, SampleRequestSampleTrial? trial, Guid? confirmedFormulaId,
        IReadOnlyList<SampleRequestWorkflowEvent>? events = null)
    {
        if (!SampleFollowUpRules.IsLegacy(request, events) && trial is null && request.SampleQuantity is not > 0 &&
            SampleRequestCustomerPriceRules.IsQuotation(request.InfoType) && events != null)
        {
            var plan = events.FirstOrDefault(e => e.EventType is SampleFlowRules.LabFinalizationRequested or
                SampleFlowRules.ChipRequested or SampleFlowRules.KgRequested or SampleFlowRules.ChipThenSampleRequested or SampleFlowRules.CompletionDeferred);
            if (plan?.EventType == SampleFlowRules.LabFinalizationRequested && ActivePrice(events, plan.FormulaId))
                return plan.FormulaId;
        }
        return (SampleFollowUpRules.IsLegacy(request, events) ? request.FormulaId :
            trial?.Status == SampleTrialStatus.Failed ? confirmedFormulaId ?? trial.FormulaId : trial?.FormulaId) ?? confirmedFormulaId ?? request.FormulaId;
    }

    internal static bool ActivePrice(IReadOnlyList<SampleRequestWorkflowEvent> events, Guid? formulaId)
    {
        var latest = events.FirstOrDefault(x => x.EventType is SampleRequestWorkflowEvents.PriceAccepted or
            SampleRequestWorkflowEvents.PriceRevoked or SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse);
        // FormulaId is historical evidence, not the identity of the TP price response.
        return latest?.EventType == SampleRequestWorkflowEvents.PriceAccepted;
    }

    internal static bool NeedsPriceAdjustment(IReadOnlyList<SampleRequestWorkflowEvent> events, DateTime? approvedPriceAt = null)
    {
        var outcome = events.FirstOrDefault(x => x.EventType is SampleRequestWorkflowEvents.PriceAccepted or
            SampleRequestWorkflowEvents.PriceRevoked or SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse);
        var choice = events.FirstOrDefault(x => x.EventType is SampleFlowRules.LowerPriceRequested or SampleFlowRules.PriceFollowUpDeferred);
        return outcome?.EventType == SampleFlowRules.PriceRejected && choice?.EventType == SampleFlowRules.LowerPriceRequested && choice.RecordedAt > outcome.RecordedAt &&
            !(approvedPriceAt > outcome.RecordedAt) &&
            !events.Any(x => x.EventType == SampleRequestWorkflowEvents.FormulaConfirmed && x.RecordedAt > outcome.RecordedAt);
    }

    internal static bool NeedsRevisedFormulaPrice(IReadOnlyList<SampleRequestWorkflowEvent> events, DateTime? approvedPriceAt = null)
    {
        var outcome = events.FirstOrDefault(x => x.EventType is SampleRequestWorkflowEvents.PriceAccepted or
            SampleRequestWorkflowEvents.PriceRevoked or SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse);
        var confirmation = events.FirstOrDefault(x => x.EventType == SampleRequestWorkflowEvents.FormulaConfirmed);
        return outcome?.EventType == SampleFlowRules.PriceRejected && confirmation?.RecordedAt > outcome.RecordedAt &&
            !(approvedPriceAt > confirmation.RecordedAt);
    }

    internal static bool NeedsPriceDecision(IReadOnlyList<SampleRequestWorkflowEvent> events, DateTime? approvedPriceAt = null)
    {
        var outcome = events.FirstOrDefault(x => x.EventType is SampleRequestWorkflowEvents.PriceAccepted or
            SampleRequestWorkflowEvents.PriceRevoked or SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse);
        var choice = events.FirstOrDefault(x => x.EventType is SampleFlowRules.LowerPriceRequested or SampleFlowRules.PriceFollowUpDeferred);
        return outcome?.EventType == SampleFlowRules.PriceRejected && !(approvedPriceAt > outcome.RecordedAt) &&
            !events.Any(x => x.EventType == SampleRequestWorkflowEvents.FormulaConfirmed && x.RecordedAt > outcome.RecordedAt) &&
            !(choice?.EventType == SampleFlowRules.LowerPriceRequested && choice.RecordedAt > outcome.RecordedAt);
    }

    internal static bool IsPriceDecisionDeferred(IReadOnlyList<SampleRequestWorkflowEvent> events)
    {
        var outcome = events.FirstOrDefault(x => x.EventType is SampleRequestWorkflowEvents.PriceAccepted or
            SampleRequestWorkflowEvents.PriceRevoked or SampleFlowRules.PriceRejected or SampleFlowRules.PriceNoResponse);
        var choice = events.FirstOrDefault(x => x.EventType is SampleFlowRules.LowerPriceRequested or SampleFlowRules.PriceFollowUpDeferred);
        return outcome?.EventType == SampleFlowRules.PriceRejected && choice?.EventType == SampleFlowRules.PriceFollowUpDeferred && choice.RecordedAt > outcome.RecordedAt;
    }
}
