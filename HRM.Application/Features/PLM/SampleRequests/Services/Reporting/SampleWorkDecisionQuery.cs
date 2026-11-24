using System.Linq.Expressions;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

internal static class SampleWorkDecisionQuery
{
    internal sealed class QueryFacts
    {
        public SampleRequestSampleTrialReportRow Row { get; init; } = null!;
        public SampleWorkFacts Facts { get; init; } = null!;
    }

    internal static IQueryable<SampleRequestSampleTrialReportRow> Enrich(
        IQueryable<SampleRequestSampleTrialReportRow> rows, IPLMDbContext db, ICRMReadDbContext crm, string currency)
        => ReadFacts(rows, db, crm, currency).Select(Projection());

    internal static IQueryable<QueryFacts> ReadFacts(
        IQueryable<SampleRequestSampleTrialReportRow> rows, IPLMDbContext db, ICRMReadDbContext crm, string currency)
    {
        var candidateRequests = DeliveryOptions.SampleDeliveryRules.RequestsWithUnsentConfirmedFormula(db.SampleRequests, db.Formulas, db.SampleRequestSampleTrials);
        var bases = rows.Select(row => new
        {
            Row = row,
            LabPaused = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == row.SampleRequest.CompanyId &&
                e.SampleRequestId == row.SampleRequest.SampleRequestId &&
                (e.EventType == SampleRequestLabPauseRules.Rejected || e.EventType == SampleRequestLabPauseRules.Resumed || e.EventType == SampleFollowUpRules.Started))
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                .Select(e => e.EventType == SampleRequestLabPauseRules.Rejected).FirstOrDefault(),
            ChipChoice = db.SampleRequestWorkflowEvents.Where(e => row.Trial != null &&
                e.CompanyId == row.SampleRequest.CompanyId && e.SampleRequestId == row.SampleRequest.SampleRequestId &&
                e.FormulaId == row.Trial.FormulaId && e.SampleRequestSampleTrialId == row.Trial.SampleRequestSampleTrialId &&
                (e.EventType == SampleFlowRules.ChipFinalizationRequested || e.EventType == SampleFlowRules.ChipFollowUpDeferred ||
                 e.EventType == SampleFlowRules.ChipRequested || e.EventType == SampleFlowRules.KgRequested || e.EventType == SampleFlowRules.CompletionDeferred))
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).FirstOrDefault(),
            NextStep = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == row.SampleRequest.CompanyId && e.SampleRequestId == row.SampleRequest.SampleRequestId &&
                (e.EventType == SampleFlowRules.ChipThenSampleRequested || e.EventType == SampleFlowRules.ChipRequested || e.EventType == SampleFlowRules.KgRequested || e.EventType == SampleFlowRules.LabFinalizationRequested ||
                 e.EventType == SampleFlowRules.ChipFinalizationRequested || e.EventType == SampleFlowRules.CompletionDeferred))
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).FirstOrDefault(),
            PriceChoice = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == row.SampleRequest.CompanyId && e.SampleRequestId == row.SampleRequest.SampleRequestId &&
                (e.EventType == SampleFlowRules.LowerPriceRequested || e.EventType == SampleFlowRules.PriceFollowUpDeferred))
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).FirstOrDefault(),
            ConfirmationRecordedAt = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == row.SampleRequest.CompanyId && e.SampleRequestId == row.SampleRequest.SampleRequestId &&
                e.EventType == SampleRequestWorkflowEvents.FormulaConfirmed).OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                .Select(e => (DateTime?)e.RecordedAt).FirstOrDefault(),
            PriceOutcome = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == row.SampleRequest.CompanyId && e.SampleRequestId == row.SampleRequest.SampleRequestId &&
                (e.EventType == SampleRequestWorkflowEvents.PriceAccepted || e.EventType == SampleRequestWorkflowEvents.PriceRevoked || e.EventType == SampleFlowRules.PriceRejected || e.EventType == SampleFlowRules.PriceNoResponse))
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).FirstOrDefault(),
            FormulaId = ((row.SampleRequest.CreatedDate < SampleFlowRules.StartsAt && !row.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == row.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? row.SampleRequest.FormulaId :
                row.Trial != null ? (row.Trial.Status == SampleTrialStatus.Failed ? row.ConfirmedFormulaId ?? row.Trial.FormulaId : row.Trial.FormulaId) : null) ?? row.ConfirmedFormulaId ?? row.SampleRequest.FormulaId
        });
        var facts = from basis in bases
                    join formula in db.Formulas.Where(f => f.IsActive) on basis.FormulaId equals (Guid?)formula.FormulaId into formulas
                    from formula in formulas.DefaultIfEmpty()
                    select new QueryFacts
                    {
                        Row = basis.Row,
                        Facts = new SampleWorkFacts
                        {
                            HasUnlinkedDeliveryCandidate = formula == null && candidateRequests.Any(id => id == basis.Row.SampleRequest.SampleRequestId),
                            Open = basis.Row.SampleRequest.Status != "Completed" && basis.Row.SampleRequest.Status != "Cancelled" && basis.Row.SampleRequest.Status != "LabDeclined",
                            LabPaused = basis.LabPaused,
                            Legacy = (basis.Row.SampleRequest.CreatedDate < SampleFlowRules.StartsAt && !basis.Row.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == basis.Row.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)),
                            Sent = basis.Row.SampleRequest.Status == "SampleSent",
                            Quotation = (basis.Row.SampleRequest.InfoType ?? "").Trim().ToLower() == "quotation" ||
                                (basis.Row.SampleRequest.InfoType ?? "").Trim().ToLower() == "báo giá",
                            RequestedKg = basis.Row.SampleRequest.SampleQuantity > 0,
                            ChipFirstPending = false,
                            ChipToSample = (basis.Row.SampleRequest.CreatedDate >= SampleFlowRules.StartsAt || basis.Row.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == basis.Row.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) &&
                                basis.Row.SampleRequest.SampleQuantity > 0 && basis.NextStep != null &&
                                basis.NextStep.EventType == SampleFlowRules.ChipThenSampleRequested && basis.Row.Trial != null &&
                                basis.Row.Trial.DeliveredSampleQuantityKg == 0 && basis.Row.Trial.SentDate != null &&
                                (basis.Row.Trial.CreatedDate >= basis.NextStep.RecordedAt || db.SampleRequestWorkflowEvents.Any(e =>
                                    e.CompanyId == basis.Row.SampleRequest.CompanyId && e.SampleRequestId == basis.Row.SampleRequest.SampleRequestId &&
                                    e.EventType == SampleRequestWorkflowEvents.SampleSent && e.SampleRequestSampleTrialId == basis.Row.Trial.SampleRequestSampleTrialId &&
                                    e.RecordedAt >= basis.NextStep.RecordedAt)),
                            HasTrial = basis.Row.Trial != null,
                            DeliveryRequested = basis.NextStep != null &&
                                (basis.NextStep.EventType == SampleFlowRules.ChipRequested || basis.NextStep.EventType == SampleFlowRules.KgRequested ||
                                 basis.NextStep.EventType == SampleFlowRules.ChipThenSampleRequested) &&
                                !db.SampleRequestWorkflowEvents.Any(e => e.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                    e.SampleRequestId == basis.Row.SampleRequest.SampleRequestId && e.EventType == SampleRequestWorkflowEvents.SampleSent &&
                                    e.RecordedAt >= basis.NextStep.RecordedAt),
                            HasSentKg = basis.FormulaId != null && db.SampleRequestSampleTrials.Any(t =>
                                t.IsActive && t.FormulaId == basis.FormulaId &&
                                t.SampleRequest.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                t.DeliveredSampleQuantityKg > 0 && t.SentDate != null),
                            PendingTrial = basis.Row.Trial != null && (basis.Row.Trial.Status == SampleTrialStatus.SampleSent ||
                                basis.Row.Trial.Status == SampleTrialStatus.WaitingCustomerFeedback || basis.Row.Trial.Status == SampleTrialStatus.PriceQuote) &&
                                !(basis.Row.Trial.SentDate != null && db.SampleRequestWorkflowEvents.Any(plan =>
                                  plan.CompanyId == basis.Row.SampleRequest.CompanyId && plan.SampleRequestId == basis.Row.SampleRequest.SampleRequestId &&
                                  plan.EventType == SampleFlowRules.KgRequested && basis.Row.Trial.CreatedDate < plan.RecordedAt &&
                                  !db.SampleRequestWorkflowEvents.Any(e => e.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                      e.SampleRequestId == basis.Row.SampleRequest.SampleRequestId &&
                                      e.EventType == SampleRequestWorkflowEvents.SampleSent &&
                                      e.SampleRequestSampleTrialId == basis.Row.Trial.SampleRequestSampleTrialId && e.RecordedAt >= plan.RecordedAt))),
                            Chip = basis.Row.Trial != null && basis.Row.Trial.DeliveredSampleQuantityKg == 0,
                            Kg = basis.Row.Trial != null && basis.Row.Trial.DeliveredSampleQuantityKg > 0,
                            Failed = basis.Row.Trial != null && basis.Row.Trial.Status == SampleTrialStatus.Failed,
                            Approved = basis.Row.Trial != null && basis.Row.Trial.Status == SampleTrialStatus.Approved,
                            UnknownSent = basis.Row.Trial != null && basis.Row.Trial.DeliveredSampleQuantityKg == null && basis.Row.Trial.SentDate != null,
                            FormulaStatus = formula != null && formula.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                formula.ProductId == basis.Row.SampleRequest.ProductId ? formula.Status : null,
                            FinalizedChip = formula != null && formula.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                formula.ProductId == basis.Row.SampleRequest.ProductId && basis.Row.HasFinalizedChip,
                            AcceptedPrice = formula != null && formula.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                formula.ProductId == basis.Row.SampleRequest.ProductId && basis.Row.HasAcceptedCustomerPrice,
                            ApprovedPrice = formula != null && formula.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                formula.ProductId == basis.Row.SampleRequest.ProductId && basis.FormulaId != null && crm.ProductPricingVersions.Where(p => p.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                p.ProductId == basis.Row.SampleRequest.ProductId && p.IsActive && p.Status == ProductPricingStatus.Approved &&
                                p.StandardSellingPrice > 0 && p.Currency == currency).OrderByDescending(p => p.Version)
                                .ThenByDescending(p => p.ApprovedAt ?? p.UpdatedDate ?? p.CreatedDate).Select(p => p.SourceFormulaId).FirstOrDefault() != null,
                            LabConfirmed = basis.Row.LabFormulaConfirmedAt.HasValue,
                            Quoted = basis.Row.SampleRequest.RealPriceQuoteDate.HasValue,
                            RequiresPriceAcceptance = (basis.Row.SampleRequest.InfoType ?? "").Trim().ToLower() == "quotation" ||
                                (basis.Row.SampleRequest.InfoType ?? "").Trim().ToLower() == "báo giá" ||
                                basis.Row.SampleRequest.RealPriceQuoteDate.HasValue || basis.PriceOutcome != null ||
                                db.SampleRequestWorkflowEvents.Any(e => e.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                    e.SampleRequestId == basis.Row.SampleRequest.SampleRequestId && e.EventType == SampleFlowRules.QuotationRequired),
                            ChipFinalizationRequested = basis.ChipChoice != null && basis.ChipChoice.EventType == SampleFlowRules.ChipFinalizationRequested,
                            ChipDecisionDeferred = basis.ChipChoice != null && basis.ChipChoice.EventType == SampleFlowRules.ChipFollowUpDeferred,
                            ChipDeliveryRequested = basis.ChipChoice != null && basis.ChipChoice.EventType == SampleFlowRules.ChipRequested,
                            CompletionDeferred = basis.NextStep != null && basis.NextStep.EventType == SampleFlowRules.CompletionDeferred && basis.NextStep.FormulaId == basis.FormulaId,
                            LabFinalizationRequested = (basis.Row.Trial == null || basis.Row.Trial.SentDate == null) && basis.PriceOutcome != null &&
                                basis.PriceOutcome.EventType == SampleRequestWorkflowEvents.PriceAccepted && basis.NextStep != null &&
                                basis.NextStep.EventType == SampleFlowRules.LabFinalizationRequested && basis.NextStep.RecordedAt >= basis.PriceOutcome.RecordedAt,
                            PriceNextStepRequired = (basis.Row.Trial == null || basis.Row.Trial.SentDate == null) &&
                                (basis.NextStep == null || basis.NextStep.EventType != SampleFlowRules.ChipThenSampleRequested) &&
                                basis.PriceOutcome != null && basis.PriceOutcome.EventType == SampleRequestWorkflowEvents.PriceAccepted &&
                                !db.SampleRequestWorkflowEvents.Any(e => e.CompanyId == basis.Row.SampleRequest.CompanyId &&
                                    e.SampleRequestId == basis.Row.SampleRequest.SampleRequestId && e.RecordedAt >= basis.PriceOutcome.RecordedAt &&
                                    (e.EventType == SampleFlowRules.ChipThenSampleRequested || e.EventType == SampleFlowRules.ChipRequested || e.EventType == SampleFlowRules.KgRequested || e.EventType == SampleFlowRules.LabFinalizationRequested)),
                            PriceAdjustmentRequired = basis.PriceOutcome != null && basis.PriceOutcome.EventType == SampleFlowRules.PriceRejected &&
                                basis.PriceChoice != null && basis.PriceChoice.EventType == SampleFlowRules.LowerPriceRequested && basis.PriceChoice.RecordedAt > basis.PriceOutcome.RecordedAt &&
                                !db.SampleRequestWorkflowEvents.Any(e => e.CompanyId == basis.Row.SampleRequest.CompanyId && e.SampleRequestId == basis.Row.SampleRequest.SampleRequestId &&
                                    e.EventType == SampleRequestWorkflowEvents.FormulaConfirmed && e.RecordedAt > basis.PriceOutcome.RecordedAt) &&
                                !(crm.ProductPricingVersions.Where(p => p.CompanyId == basis.Row.SampleRequest.CompanyId && p.ProductId == basis.Row.SampleRequest.ProductId && p.IsActive &&
                                    p.Status == ProductPricingStatus.Approved && p.StandardSellingPrice > 0 && p.Currency == currency)
                                    .OrderByDescending(p => p.Version).ThenByDescending(p => p.ApprovedAt ?? p.UpdatedDate ?? p.CreatedDate)
                                    .Select(p => (DateTime?)(p.ApprovedAt ?? p.UpdatedDate ?? p.CreatedDate)).FirstOrDefault() > basis.PriceOutcome.RecordedAt),
                            PriceDecisionDeferred = basis.PriceChoice != null && basis.PriceOutcome != null && basis.PriceChoice.EventType == SampleFlowRules.PriceFollowUpDeferred && basis.PriceChoice.RecordedAt > basis.PriceOutcome.RecordedAt,
                            PriceDecisionRequired = basis.PriceOutcome != null && basis.PriceOutcome.EventType == SampleFlowRules.PriceRejected &&
                                !(basis.PriceChoice != null && basis.PriceChoice.EventType == SampleFlowRules.LowerPriceRequested && basis.PriceChoice.RecordedAt > basis.PriceOutcome.RecordedAt) &&
                                !(basis.ConfirmationRecordedAt > basis.PriceOutcome.RecordedAt) &&
                                !(crm.ProductPricingVersions.Where(p => p.CompanyId == basis.Row.SampleRequest.CompanyId && p.ProductId == basis.Row.SampleRequest.ProductId && p.IsActive &&
                                    p.Status == ProductPricingStatus.Approved && p.StandardSellingPrice > 0 && p.Currency == currency)
                                    .OrderByDescending(p => p.Version).ThenByDescending(p => p.ApprovedAt ?? p.UpdatedDate ?? p.CreatedDate)
                                    .Select(p => (DateTime?)(p.ApprovedAt ?? p.UpdatedDate ?? p.CreatedDate)).FirstOrDefault() > basis.PriceOutcome.RecordedAt),
                            RevisedFormulaNeedsPrice = basis.PriceOutcome != null && basis.PriceOutcome.EventType == SampleFlowRules.PriceRejected &&
                                basis.ConfirmationRecordedAt > basis.PriceOutcome.RecordedAt &&
                                !(crm.ProductPricingVersions.Where(p => p.CompanyId == basis.Row.SampleRequest.CompanyId && p.ProductId == basis.Row.SampleRequest.ProductId && p.IsActive &&
                                    p.Status == ProductPricingStatus.Approved && p.StandardSellingPrice > 0 && p.Currency == currency)
                                    .OrderByDescending(p => p.Version).ThenByDescending(p => p.ApprovedAt ?? p.UpdatedDate ?? p.CreatedDate)
                                    .Select(p => (DateTime?)(p.ApprovedAt ?? p.UpdatedDate ?? p.CreatedDate)).FirstOrDefault() > basis.ConfirmationRecordedAt)
                        }
                    };
        return facts;
    }

    private static Expression<Func<QueryFacts, SampleRequestSampleTrialReportRow>> Projection()
    {
        Expression<Func<QueryFacts, SampleRequestSampleTrialReportRow>> template = x => new SampleRequestSampleTrialReportRow
        {
            SampleRequest = x.Row.SampleRequest, Trial = x.Row.Trial, TrialCount = x.Row.TrialCount,
            ConfirmedFormulaId = x.Row.ConfirmedFormulaId, LabFormulaConfirmedAt = x.Row.LabFormulaConfirmedAt,
            HasPriceFeedback = x.Row.HasPriceFeedback, HasAcceptedCustomerPrice = x.Row.HasAcceptedCustomerPrice, HasFinalizedChip = x.Row.HasFinalizedChip,
            NeedsSchedule = x.Facts.Open && !x.Facts.LabPaused && (x.Facts.Quotation && !x.Facts.RequestedKg
                ? x.Row.SampleRequest.ExpectedPriceQuoteDate == null : x.Row.SampleRequest.ExpectedDeliveryDate == null)
        };
        var parameter = template.Parameters[0];
        var decision = SampleWorkDecisionRules.Replace(SampleWorkDecisionRules.Expression.Body,
            SampleWorkDecisionRules.Expression.Parameters[0], Expression.Property(parameter, nameof(QueryFacts.Facts)));
        var body = (MemberInitExpression)template.Body;
        var facts = Expression.Property(parameter, nameof(QueryFacts.Facts));
        var labRank = SampleWorkTaskPriority.LabRank;
        var rank = SampleWorkDecisionRules.Replace(labRank.Body, labRank.Parameters[0], facts);
        var action = SampleWorkDecisionRules.Action;
        var code = SampleWorkDecisionRules.Replace(action.Body, action.Parameters[0], facts);
        rank = SampleWorkDecisionRules.Replace(rank, labRank.Parameters[1], code);
        var price = SampleWorkDecisionRules.Replace(SampleWorkDecisionRules.SalePricePending.Body, SampleWorkDecisionRules.SalePricePending.Parameters[0], facts);
        var feedback = SampleWorkDecisionRules.Replace(SampleWorkDecisionRules.SaleFeedbackPending.Body, SampleWorkDecisionRules.SaleFeedbackPending.Parameters[0], facts);
        return Expression.Lambda<Func<QueryFacts, SampleRequestSampleTrialReportRow>>(Expression.MemberInit(body.NewExpression,
            body.Bindings.Concat([
                Expression.Bind(typeof(SampleRequestSampleTrialReportRow).GetProperty(nameof(SampleRequestSampleTrialReportRow.WorkDecision))!, decision),
                Expression.Bind(typeof(SampleRequestSampleTrialReportRow).GetProperty(nameof(SampleRequestSampleTrialReportRow.LabTaskRank))!,
                    rank),
                Expression.Bind(typeof(SampleRequestSampleTrialReportRow).GetProperty(nameof(SampleRequestSampleTrialReportRow.HasSalePriceWork))!, price),
                Expression.Bind(typeof(SampleRequestSampleTrialReportRow).GetProperty(nameof(SampleRequestSampleTrialReportRow.HasSaleFeedbackWork))!, feedback)
            ])), parameter);
    }
}
