using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

/// <summary>SQL read-only priority matching the work-list assignment and due-date rules.</summary>
internal static class SampleRequestWorkSortQuery
{
    internal static IQueryable<SampleRequestSampleTrialReportRow> Apply(
        IQueryable<SampleRequestSampleTrialReportRow> rows, SampleRequestWorkSort? sort, bool workflowDetail,
        SampleRequestWorkAudience? audience, DateTime day, bool technical, bool sale, bool pricing, bool schedule,
        Guid[] saleOwners, IQueryable<SampleRequest> writable)
    {
        // Workflow đơn TP tính WorkDecision sau khi materialize, không có cột rank để sắp xếp.
        if (workflowDetail || sort is null) return rows;
        if (!Enum.IsDefined(sort.Value)) throw new ArgumentException("Invalid sample work sort.");
        if (sort == SampleRequestWorkSort.Newest) return OrderNewest(rows);
        var ranked = Enrich(rows, audience, day, technical, sale, pricing, schedule, saleOwners, writable);
        return sort == SampleRequestWorkSort.CustomerCode ? OrderCustomerCode(ranked) : OrderPriority(ranked);
    }

    internal static IQueryable<SampleRequestSampleTrialReportRow> Enrich(
        IQueryable<SampleRequestSampleTrialReportRow> rows, SampleRequestWorkAudience? audience,
        DateTime day, bool technical, bool sale, bool pricing, bool schedule,
        Guid[] saleOwners, IQueryable<SampleRequest> writable)
    {
        var facts = rows.Select(x => new
        {
            Row = x,
            Mine = audience == SampleRequestWorkAudience.LabAdmin && schedule && x.WorkDecision!.Code != "ResumeAfterLabRejection" ||
                x.WorkDecision!.Role == "Lab" && technical && audience != SampleRequestWorkAudience.Sale ||
                (x.WorkDecision.Role == "Sale" || x.HasSalePriceWork || x.HasSaleFeedbackWork) && sale &&
                audience != SampleRequestWorkAudience.Lab && audience != SampleRequestWorkAudience.LabAdmin &&
                (x.WorkDecision.Code == "ResumeAfterLabRejection" || saleOwners.Contains(x.SampleRequest.ManagerBy) && writable.Any(w => w.SampleRequestId == x.SampleRequest.SampleRequestId)) ||
                x.WorkDecision.Role == "President" && pricing || x.WorkDecision.Role == "LabAdmin" && schedule &&
                (audience == null || audience == SampleRequestWorkAudience.LabAdmin),
            Due = (x.SampleRequest.SampleQuantity == null || x.SampleRequest.SampleQuantity <= 0) &&
                ((x.SampleRequest.InfoType ?? "").Trim().ToLower() == "quotation" ||
                 (x.SampleRequest.InfoType ?? "").Trim().ToLower() == "báo giá")
                ? x.SampleRequest.ExpectedPriceQuoteDate
                : x.SampleRequest.ExpectedDeliveryDate ?? x.SampleRequest.RequestDeliveryDate ??
                    (x.SampleRequest.LabReceivedDate.HasValue && x.SampleRequest.NumberDeliverySampleDate >= 0 &&
                     x.SampleRequest.NumberDeliverySampleDate <= (DateTime.MaxValue.Date - x.SampleRequest.LabReceivedDate.Value.Date).TotalDays
                        ? (DateTime?)x.SampleRequest.LabReceivedDate.Value.Date.AddDays(x.SampleRequest.NumberDeliverySampleDate.GetValueOrDefault()) : null)
        });
        return facts.Select(x => new SampleRequestSampleTrialReportRow
        {
            SampleRequest = x.Row.SampleRequest, Trial = x.Row.Trial, TrialCount = x.Row.TrialCount,
            LabFormulaConfirmedAt = x.Row.LabFormulaConfirmedAt, ConfirmedFormulaId = x.Row.ConfirmedFormulaId,
            HasPriceFeedback = x.Row.HasPriceFeedback, HasAcceptedCustomerPrice = x.Row.HasAcceptedCustomerPrice,
            HasFinalizedChip = x.Row.HasFinalizedChip, WorkDecision = x.Row.WorkDecision, NeedsSchedule = x.Row.NeedsSchedule,
            HasSalePriceWork = x.Row.HasSalePriceWork, HasSaleFeedbackWork = x.Row.HasSaleFeedbackWork,
            WaitsForPriceBeforeSample = x.Row.WaitsForPriceBeforeSample,
            WorkSortRank = x.Row.SampleRequest.Status == "Completed" ? 4 : (x.Row.SampleRequest.Status == "Cancelled" || x.Row.SampleRequest.Status == "LabDeclined") ? 5 : !x.Mine ? 3 :
                audience == SampleRequestWorkAudience.Lab ? x.Row.LabTaskRank :
                audience == SampleRequestWorkAudience.LabAdmin ? (x.Row.LabTaskRank < 0 ? x.Row.LabTaskRank : 0) :
                audience == SampleRequestWorkAudience.Sale ?
                    (x.Row.WorkDecision!.Code == "CompleteRequest" ? 0 : x.Row.WorkDecision.Code == "FollowUpCustomer" || x.Row.HasSaleFeedbackWork ? 1 : 2) : 0,
            WorkSortDue = x.Due,
            WorkSortOverdue = x.Row.SampleRequest.Status != "Completed" && x.Row.SampleRequest.Status != "Cancelled" &&
                x.Row.WorkDecision!.Role == "Lab" && x.Due < day
        });
    }

    // Same ranking as the SQL projection above; used by the scoped facts stream BEFORE paging.
    internal static int Rank(string status, bool mine, SampleRequestWorkAudience? audience, string code, int labRank)
        => status == "Completed" ? 4 : (status == "Cancelled" || status == "LabDeclined") ? 5 : !mine ? 3 : audience switch
        {
            SampleRequestWorkAudience.Lab => labRank,
            SampleRequestWorkAudience.LabAdmin => Math.Min(labRank, 0),
            SampleRequestWorkAudience.Sale => code == "CompleteRequest" ? 0 : code == "FollowUpCustomer" ? 1 : 2,
            _ => 0
        };

    internal static IOrderedQueryable<SampleRequestSampleTrialReportRow> OrderPriority(IQueryable<SampleRequestSampleTrialReportRow> rows, Guid[]? pinnedRequestIds = null)
        => rows.OrderByDescending(x => (pinnedRequestIds ?? Array.Empty<Guid>()).Contains(x.SampleRequest.SampleRequestId)).ThenBy(x => x.WorkSortRank).ThenByDescending(x => x.WorkSortOverdue)
            .ThenBy(x => x.WorkSortDue == null).ThenBy(x => x.WorkSortDue).ThenBy(x => x.SampleRequest.SampleRequestId);

    internal static IOrderedQueryable<SampleRequestSampleTrialReportRow> OrderNewest(IQueryable<SampleRequestSampleTrialReportRow> rows, Guid[]? pinnedRequestIds = null)
        => rows.OrderByDescending(x => (pinnedRequestIds ?? Array.Empty<Guid>()).Contains(x.SampleRequest.SampleRequestId))
            .ThenByDescending(x => x.SampleRequest.CreatedDate).ThenByDescending(x => x.SampleRequest.SampleRequestId);

    internal static IOrderedQueryable<SampleRequestSampleTrialReportRow> OrderCustomerCode(IQueryable<SampleRequestSampleTrialReportRow> rows, Guid[]? pinnedRequestIds = null)
        => rows.OrderByDescending(x => (pinnedRequestIds ?? Array.Empty<Guid>()).Contains(x.SampleRequest.SampleRequestId)).ThenBy(x => x.SampleRequest.Customer.ExternalId == null || x.SampleRequest.Customer.ExternalId == "")
            .ThenBy(x => x.SampleRequest.Customer.ExternalId).ThenBy(x => x.SampleRequest.CustomerId)
            .ThenBy(x => x.WorkSortRank).ThenByDescending(x => x.WorkSortOverdue)
            .ThenBy(x => x.WorkSortDue == null).ThenBy(x => x.WorkSortDue).ThenBy(x => x.SampleRequest.SampleRequestId);
}
