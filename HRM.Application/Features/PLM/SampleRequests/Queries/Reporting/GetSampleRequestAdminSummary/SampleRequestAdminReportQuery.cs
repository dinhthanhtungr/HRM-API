using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

internal sealed class SampleRequestAdminReportRecord
{
    public Guid SampleRequestId { get; init; }
    public Guid ProductId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchName { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? ReceivedDate { get; init; }
    public DateTime? CompletedDate { get; init; }
    public DateTime? LabCompletedDate { get; init; }
    public string LabCompletedKind { get; init; } = "other";
    public DateTime? PriceAcceptedDate { get; init; }
    public DateTime? ChipAcceptedDate { get; init; }
    public DateTime? SampleAcceptedDate { get; init; }
    public DateTime? LabDeclinedDate { get; init; }
    public DateTime? CancelledDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool HasFormula { get; init; }
    public bool IsLabPaused { get; init; }
}

/// <summary>Only persisted milestones are used; UpdatedDate is never guessed as a terminal date.</summary>
internal static class SampleRequestAdminReportQuery
{
    public static bool IsValidPeriod(DateOnly from, DateOnly to)
        => from != default && to != default && to >= from && to != DateOnly.MaxValue && to.DayNumber - from.DayNumber <= 366;

    public static IQueryable<SampleRequestAdminReportRecord> Build(IQueryable<SampleRequest> visible, IPLMReadDbContext db,
        IQueryable<ProductPricingVersion>? prices = null)
    {
        var hasPrices = prices is not null;
        return from x in SampleRequestWorkScope.Apply(visible, db.SampleRequestWorkflowEvents)
        let sent = db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == x.SampleRequestId &&
            t.SampleRequest.CompanyId == x.CompanyId && t.IsActive &&
            (!(x.SampleQuantity > 0) || t.DeliveredSampleQuantityKg > 0)).Select(t => t.SentDate).Max()
        let basis = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.CompanyId && e.SampleRequestId == x.SampleRequestId &&
            e.EventType == SampleRequestWorkflowEvents.FormulaConfirmed && e.FormulaId != null)
            .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).Select(e => e.FormulaId).FirstOrDefault() ?? x.FormulaId
        // Coalesce BEFORE testing presence. EF can otherwise push HasValue into the scalar
        // SELECT; no matching price row yields SQL NULL (not false), discarding a real delivery.
        let approvedAt = !hasPrices ? DateTime.MinValue : prices!.Where(p => p.CompanyId == x.CompanyId &&
            p.ProductId == x.ProductId && p.IsActive && p.Status == ProductPricingStatus.Approved &&
            p.Currency == "VND" && p.StandardSellingPrice > 0 && p.SourceFormulaId != null && p.SourceFormulaId == basis &&
            db.Formulas.Any(f => f.FormulaId == p.SourceFormulaId && f.CompanyId == x.CompanyId && f.IsActive && f.ProductId == x.ProductId) &&
            !(x.SampleQuantity > 0) && (x.InfoType == "Báo giá" || x.InfoType == "Quotation"))
            .OrderByDescending(p => p.Version).Select(p => p.ApprovedAt).FirstOrDefault() ?? DateTime.MinValue
        let approved = approvedAt == DateTime.MinValue ? (DateTime?)null : approvedAt
        let workRequested = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.CompanyId && e.SampleRequestId == x.SampleRequestId &&
            (e.EventType == SampleFlowRules.LowerPriceRequested || e.EventType == SampleFlowRules.KgRequested ||
             e.EventType == SampleFlowRules.ChipThenSampleRequested || e.EventType == SampleFlowRules.ChipRequested || e.EventType == SampleFlowRules.ChipReopened ||
             e.EventType == SampleFlowRules.LabFinalizationRequested))
            .Select(e => e.OccurredAt ?? (DateTime?)e.RecordedAt).Max()
        let failed = db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == x.SampleRequestId &&
            t.SampleRequest.CompanyId == x.CompanyId && t.IsActive && (t.Status == SampleTrialStatus.Failed || t.Status == SampleTrialStatus.Draft))
            .Select(t => t.Status == SampleTrialStatus.Failed ? t.CustomerReplyDate ?? t.UpdatedDate ?? t.CreatedDate : (DateTime?)t.CreatedDate).Max()
        let reset = !workRequested.HasValue ? failed : !failed.HasValue || workRequested >= failed ? workRequested : failed
        let delivered = !sent.HasValue ? approved : !approved.HasValue || sent >= approved ? sent : approved
        let terminal = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.CompanyId && e.SampleRequestId == x.SampleRequestId &&
            !(x.SampleQuantity > 0) && e.EventType == SampleFlowRules.CompletedWithoutTrial).Select(e => e.OccurredAt ?? (DateTime?)e.RecordedAt).Max()
        let done = !delivered.HasValue ? terminal : !terminal.HasValue || delivered >= terminal ? delivered : terminal
        let sentKind = db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == x.SampleRequestId &&
            t.SampleRequest.CompanyId == x.CompanyId && t.IsActive && t.SentDate != null &&
            (!(x.SampleQuantity > 0) || t.DeliveredSampleQuantityKg > 0))
            .OrderByDescending(t => t.SentDate).ThenByDescending(t => t.TrialNo).Select(t => t.DeliveredSampleQuantityKg > 0 ? "sample" :
                t.DeliveredSampleQuantityKg == 0 ? "chip" : "other").FirstOrDefault()
        select new SampleRequestAdminReportRecord
        {
            SampleRequestId = x.SampleRequestId,
            ProductId = x.ProductId,
            ExternalId = x.ExternalId,
            BranchId = x.BranchId,
            BranchCode = x.Branch != null ? x.Branch!.Code : null,
            BranchName = x.Branch != null ? x.Branch!.Name : null,
            CreatedDate = x.CreatedDate,
            ReceivedDate = x.LabReceivedDate,
            Status = x.Status,
            IsLabPaused = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.CompanyId && e.SampleRequestId == x.SampleRequestId &&
                (e.EventType == SampleRequestLabPauseRules.Rejected || e.EventType == SampleRequestLabPauseRules.Resumed))
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                .Select(e => e.EventType == SampleRequestLabPauseRules.Rejected).FirstOrDefault(),
            LabCompletedDate = done.HasValue && (!reset.HasValue || done >= reset) ? done : null,
            LabCompletedKind = sent.HasValue && sent == done ? sentKind ?? "other" :
                approved.HasValue && approved == done ? "quotation" : terminal.HasValue && terminal == done ?
                    (x.InfoType == "Báo giá" || x.InfoType == "Quotation" ? "quotation" : "chip") : "other",
            PriceAcceptedDate = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.CompanyId && e.SampleRequestId == x.SampleRequestId &&
                (e.EventType == SampleRequestWorkflowEvents.PriceAccepted || e.EventType == "CustomerPriceConfirmed" ||
                 e.EventType == SampleRequestWorkflowEvents.PriceRevoked || e.EventType == "CustomerPriceConfirmationRevoked" ||
                 e.EventType == SampleFlowRules.PriceRejected || e.EventType == SampleFlowRules.PriceNoResponse))
                .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                .Select(e => e.EventType == SampleRequestWorkflowEvents.PriceAccepted || e.EventType == "CustomerPriceConfirmed"
                    ? e.OccurredAt ?? (DateTime?)e.RecordedAt : null).FirstOrDefault(),
            ChipAcceptedDate = db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == x.SampleRequestId &&
                t.SampleRequest.CompanyId == x.CompanyId && t.IsActive && t.DeliveredSampleQuantityKg == 0)
                .OrderByDescending(t => t.TrialNo).Select(t => t.Status == SampleTrialStatus.Approved ? t.CustomerReplyDate : null).FirstOrDefault(),
            SampleAcceptedDate = db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == x.SampleRequestId &&
                t.SampleRequest.CompanyId == x.CompanyId && t.IsActive && t.DeliveredSampleQuantityKg > 0)
                .OrderByDescending(t => t.TrialNo).Select(t => t.Status == SampleTrialStatus.Approved ? t.CustomerReplyDate : null).FirstOrDefault(),
            HasFormula = db.Formulas.Any(f => f.CompanyId == x.CompanyId && f.ProductId == x.ProductId && f.IsActive),
            CompletedDate = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.CompanyId && e.SampleRequestId == x.SampleRequestId &&
                e.EventType == Services.Workflow.SampleFlowRules.CompletedWithoutTrial)
                .Select(e => e.OccurredAt ?? (DateTime?)e.RecordedAt).Max()
                ?? db.SampleRequestSampleTrials.Where(t => t.IsActive &&
                t.SampleRequestId == x.SampleRequestId && t.SampleRequest.CompanyId == x.CompanyId &&
                t.Status == SampleTrialStatus.Approved &&
                (x.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt || t.DeliveredSampleQuantityKg > 0))
                .Select(t => t.CustomerReplyDate).Max(),
            LabDeclinedDate = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.CompanyId &&
                e.SampleRequestId == x.SampleRequestId && e.EventType == SampleRequestWorkflowEvents.LabDeclined)
                .Select(e => e.OccurredAt ?? (DateTime?)e.RecordedAt).Max(),
            CancelledDate = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.CompanyId &&
                e.SampleRequestId == x.SampleRequestId && e.EventType == SampleRequestWorkflowEvents.RequestCancelled)
                .Select(e => e.OccurredAt ?? (DateTime?)e.RecordedAt).Max()
                ?? db.SampleRequestSampleTrials.Where(t => t.IsActive && t.SampleRequestId == x.SampleRequestId &&
                    t.SampleRequest.CompanyId == x.CompanyId && t.Status == SampleTrialStatus.Cancelled)
                    .Select(t => t.CustomerReplyDate).Max()
        };
    }

    public static IQueryable<SampleRequestAdminReportRecord> Filter(
        IQueryable<SampleRequestAdminReportRecord> rows, SampleRequestAdminMetric metric, DateTime start, DateTime end)
        => metric switch
        {
            SampleRequestAdminMetric.CompletedQuotation => Filter(rows, SampleRequestAdminMetric.Completed, start, end).Where(x => x.LabCompletedKind == "quotation"),
            SampleRequestAdminMetric.CompletedChip => Filter(rows, SampleRequestAdminMetric.Completed, start, end).Where(x => x.LabCompletedKind == "chip"),
            SampleRequestAdminMetric.CompletedSample => Filter(rows, SampleRequestAdminMetric.Completed, start, end).Where(x => x.LabCompletedKind == "sample"),
            SampleRequestAdminMetric.CompletedOther => Filter(rows, SampleRequestAdminMetric.Completed, start, end).Where(x => x.LabCompletedKind == "other"),
            SampleRequestAdminMetric.InProgress => rows.Where(x => x.CreatedDate < end && !x.IsLabPaused && x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "LabDeclined" && x.HasFormula && x.LabCompletedDate == null),
            SampleRequestAdminMetric.Pending => rows.Where(x => x.CreatedDate < end &&
                (x.IsLabPaused || (x.Status == "New" || x.Status == "InProgress") && !x.HasFormula)),
            // Total incoming requests in the period, independent of recording receipt of a physical standard.
            SampleRequestAdminMetric.Received => rows.Where(x => x.CreatedDate >= start && x.CreatedDate < end),
            SampleRequestAdminMetric.Completed => rows.Where(x => x.Status != "Cancelled" && x.Status != "LabDeclined" && x.LabCompletedDate >= start && x.LabCompletedDate < end),
            SampleRequestAdminMetric.PriceAccepted => rows.Where(x => x.PriceAcceptedDate >= start && x.PriceAcceptedDate < end),
            SampleRequestAdminMetric.ChipAccepted => rows.Where(x => x.ChipAcceptedDate >= start && x.ChipAcceptedDate < end),
            SampleRequestAdminMetric.SampleAccepted => rows.Where(x => x.SampleAcceptedDate >= start && x.SampleAcceptedDate < end),
            SampleRequestAdminMetric.LabDeclined => rows.Where(x => x.Status == "LabDeclined" && x.LabDeclinedDate >= start && x.LabDeclinedDate < end),
            SampleRequestAdminMetric.Cancelled => rows.Where(x => x.Status == "Cancelled" && x.CancelledDate >= start && x.CancelledDate < end),
            _ => rows.Where(x => x.CreatedDate < end &&
                (x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "LabDeclined" ||
                 x.Status == "Completed" && x.CompletedDate >= end ||
                 x.Status == "Cancelled" && x.CancelledDate >= end ||
                 x.Status == "LabDeclined" && x.LabDeclinedDate >= end))
        };
}
