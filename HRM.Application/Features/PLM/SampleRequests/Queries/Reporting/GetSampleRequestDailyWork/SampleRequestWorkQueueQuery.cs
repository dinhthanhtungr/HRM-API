using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

/// <summary>Filters the canonical decision in SQL before count/paging.</summary>
internal static class SampleRequestWorkQueueQuery
{
    internal static IQueryable<HRM.Domain.Entities.SampleRequestSchema.SampleRequest> ApplySaleOwnership(
        IQueryable<HRM.Domain.Entities.SampleRequestSchema.SampleRequest> visible,
        IQueryable<HRM.Domain.Entities.SampleRequestSchema.SampleRequest> writable,
        Guid[] allowedOwners)
        => visible.Where(x => allowedOwners.Contains(x.ManagerBy) &&
            writable.Any(candidate => candidate.SampleRequestId == x.SampleRequestId));

    public static IQueryable<SampleRequestSampleTrialReportRow> Apply(
        IQueryable<SampleRequestSampleTrialReportRow> rows, GetSampleRequestSampleTrialsQuery request, ICRMReadDbContext crm)
    {
        var bucket = request.WorkBucket;
        if (bucket == SampleRequestWorkBucket.All) return rows;
        if (bucket == SampleRequestWorkBucket.Completed) return rows.Where(x => x.SampleRequest.Status == "Completed");
        if (bucket == SampleRequestWorkBucket.Cancelled) return rows.Where(x => x.SampleRequest.Status == "Cancelled");
        var open = rows.Where(x => x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled");
        if (bucket == SampleRequestWorkBucket.LabProgress) return open;
        if (bucket == SampleRequestWorkBucket.NewRequestRequired) return open.Where(x => x.WorkDecision!.Code == "CreateNewRequest");
        if (bucket == SampleRequestWorkBucket.Rework) return open.Where(x => x.WorkDecision!.Code == "ReworkFormula");
        if (bucket == SampleRequestWorkBucket.WaitingLab) return open.Where(x => x.WorkDecision!.Role == "Lab");
        if (bucket == SampleRequestWorkBucket.WaitingSale) return open.Where(x => x.WorkDecision!.Role == "Sale");
        if (bucket == SampleRequestWorkBucket.WaitingPrice) return open.Where(x => x.WorkDecision!.Role == "President");
        if (bucket == SampleRequestWorkBucket.FeedbackReceived) return rows.Where(x => x.HasPriceFeedback || x.Trial != null && x.Trial.CustomerReplyDate != null);
        if (bucket == SampleRequestWorkBucket.Overdue)
        {
            var start = (request.DailyWorkDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
            return open.Where(x => x.WorkDecision!.Role == "Lab" &&
                ((x.SampleRequest.SampleQuantity == null || x.SampleRequest.SampleQuantity <= 0) &&
                 ((x.SampleRequest.InfoType ?? "").Trim().ToLower() == "quotation" || (x.SampleRequest.InfoType ?? "").Trim().ToLower() == "báo giá")
                    ? x.SampleRequest.ExpectedPriceQuoteDate < start :
                    (x.SampleRequest.ExpectedDeliveryDate ?? x.SampleRequest.RequestDeliveryDate ??
                     (x.SampleRequest.LabReceivedDate.HasValue && x.SampleRequest.NumberDeliverySampleDate >= 0 &&
                      x.SampleRequest.NumberDeliverySampleDate <= (DateTime.MaxValue.Date - x.SampleRequest.LabReceivedDate.Value.Date).TotalDays
                         ? (DateTime?)x.SampleRequest.LabReceivedDate.Value.Date.AddDays(x.SampleRequest.NumberDeliverySampleDate.GetValueOrDefault())
                         : null)) < start));
        }
        if (bucket is SampleRequestWorkBucket.ActionRequired or SampleRequestWorkBucket.MyActionRequired)
            return request.WorkAudience switch
            {
                SampleRequestWorkAudience.LabAdmin => open.Where(x => x.WorkDecision!.Role == "LabAdmin" || x.WorkDecision.Role == "Lab" && x.NeedsSchedule),
                SampleRequestWorkAudience.Lab => open.Where(x => x.WorkDecision!.Role == "Lab"),
                _ => open.Where(x => x.WorkDecision!.Role == "Sale")
            };
        return rows;
    }
}
