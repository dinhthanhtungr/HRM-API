using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

internal sealed class SampleRequestAdminReportRecord
{
    public Guid SampleRequestId { get; init; }
    public Guid BranchId { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchName { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? ReceivedDate { get; init; }
    public DateTime? CompletedDate { get; init; }
    public DateTime? CancelledDate { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>Only persisted milestones are used; UpdatedDate is never guessed as a terminal date.</summary>
internal static class SampleRequestAdminReportQuery
{
    public static bool IsValidPeriod(DateOnly from, DateOnly to)
        => from != default && to != default && to >= from && to != DateOnly.MaxValue && to.DayNumber - from.DayNumber <= 366;

    public static IQueryable<SampleRequestAdminReportRecord> Build(IQueryable<SampleRequest> visible, IPLMReadDbContext db)
        => SampleRequestWorkScope.Apply(visible, db.SampleRequestWorkflowEvents).Select(x => new SampleRequestAdminReportRecord
        {
            SampleRequestId = x.SampleRequestId,
            BranchId = x.BranchId,
            BranchCode = x.Branch != null ? x.Branch.Code : null,
            BranchName = x.Branch != null ? x.Branch.Name : null,
            CreatedDate = x.CreatedDate,
            ReceivedDate = x.LabReceivedDate,
            Status = x.Status,
            CompletedDate = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.CompanyId && e.SampleRequestId == x.SampleRequestId &&
                e.EventType == Services.Workflow.SampleFlowRules.CompletedWithoutTrial)
                .Select(e => e.OccurredAt ?? (DateTime?)e.RecordedAt).Max()
                ?? db.SampleRequestSampleTrials.Where(t => t.IsActive &&
                t.SampleRequestId == x.SampleRequestId && t.SampleRequest.CompanyId == x.CompanyId &&
                t.Status == SampleTrialStatus.Approved &&
                (x.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt || t.DeliveredSampleQuantityKg > 0))
                .Select(t => t.CustomerReplyDate).Max(),
            CancelledDate = db.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.CompanyId &&
                e.SampleRequestId == x.SampleRequestId && e.EventType == SampleRequestWorkflowEvents.RequestCancelled)
                .Select(e => e.OccurredAt ?? (DateTime?)e.RecordedAt).Max()
                ?? db.SampleRequestSampleTrials.Where(t => t.IsActive && t.SampleRequestId == x.SampleRequestId &&
                    t.SampleRequest.CompanyId == x.CompanyId && t.Status == SampleTrialStatus.Cancelled)
                    .Select(t => t.CustomerReplyDate).Max()
        });

    public static IQueryable<SampleRequestAdminReportRecord> Filter(
        IQueryable<SampleRequestAdminReportRecord> rows, SampleRequestAdminMetric metric, DateTime start, DateTime end)
        => metric switch
        {
            SampleRequestAdminMetric.Received => rows.Where(x => x.ReceivedDate >= start && x.ReceivedDate < end),
            SampleRequestAdminMetric.Completed => rows.Where(x => x.Status == "Completed" && x.CompletedDate >= start && x.CompletedDate < end),
            SampleRequestAdminMetric.Cancelled => rows.Where(x => x.Status == "Cancelled" && x.CancelledDate >= start && x.CancelledDate < end),
            _ => rows.Where(x => x.CreatedDate < end &&
                (x.Status != "Completed" && x.Status != "Cancelled" ||
                 x.Status == "Completed" && x.CompletedDate >= end ||
                 x.Status == "Cancelled" && x.CancelledDate >= end))
        };
}
