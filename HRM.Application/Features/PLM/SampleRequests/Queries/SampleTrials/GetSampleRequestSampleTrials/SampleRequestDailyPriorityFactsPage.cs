using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

/// <summary>One facts-only pass over scoped/filtered grouped queues; count groups and reuse selected facts.</summary>
internal static class SampleRequestDailyPriorityFactsPage
{
    internal static bool CanRead(GetSampleRequestSampleTrialsQuery request)
        => request.SuggestionKeyword is null && (request.GroupByCustomer ||
               request.WorkBucket is SampleRequestWorkBucket.MyActionRequired or SampleRequestWorkBucket.ActionRequired) && request.WorkSort == SampleRequestWorkSort.WorkPriority &&
           request.WorkAudience.HasValue && !request.IncludeTrialHistory &&
           (request.WorkBucket is SampleRequestWorkBucket.All or SampleRequestWorkBucket.Rework or
                SampleRequestWorkBucket.NewRequestRequired or SampleRequestWorkBucket.WaitingLab or
                SampleRequestWorkBucket.WaitingSale or SampleRequestWorkBucket.WaitingPrice or
                SampleRequestWorkBucket.LabProgress or SampleRequestWorkBucket.Overdue or
                SampleRequestWorkBucket.FeedbackReceived or SampleRequestWorkBucket.Completed or SampleRequestWorkBucket.Cancelled or SampleRequestWorkBucket.LabDeclined ||
            request.WorkAudience == SampleRequestWorkAudience.Sale &&
                request.WorkBucket is SampleRequestWorkBucket.MyActionRequired or SampleRequestWorkBucket.ActionRequired ||
            request.WorkAudience is SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin && request.WorkBucket == SampleRequestWorkBucket.ActionRequired);

    internal sealed record Access(SampleRequestWorkAudience? Audience, bool Technical, bool Sale, bool Pricing,
        bool Schedule, Guid[] SaleOwners, string? RequiredRole = null,
        SampleRequestWorkBucket Bucket = SampleRequestWorkBucket.All, Guid[]? PinnedRequestIds = null,
        string[]? PriorityTasks = null, bool GroupByCustomer = true);
    internal sealed class ReadRow
    {
        public Guid RequestId { get; init; }
        public DateTime CreatedDate { get; init; }
        public Guid CustomerId { get; init; }
        public SampleRequestDailyPayloadPage.State State { get; init; } = null!;
        public string Status { get; init; } = "";
        public DateTime? Due { get; init; }
        public bool SaleWritable { get; init; }
        public bool HasFeedback { get; init; }
    }
    private sealed record Candidate(ReadRow Row, int Rank, bool Overdue, bool Pinned, bool Preferred);
    private sealed record CustomerGroup(Guid Id, int TieOrder, List<Candidate> Rows, Candidate Best);
    internal sealed record Result(Guid[] CustomerIds, SampleRequestDailyPayloadPage Payload, int TotalCustomers)
    {
        internal IReadOnlyDictionary<string, int>? TaskCounts { get; init; }
    }

    internal static IQueryable<ReadRow> ReadRows(IQueryable<SampleRequestSampleTrialReportRow> filtered,
        IPLMDbContext db, ICRMReadDbContext crm, string currency, Access access, IQueryable<SampleRequest> writable)
        => SampleWorkDecisionQuery.ReadFacts(filtered, db, crm, currency)
            .OrderBy(x => x.Row.SampleRequest.Customer.ExternalId).ThenBy(x => x.Row.SampleRequest.CustomerId)
            .Select(x => new ReadRow
            {
                RequestId = x.Row.SampleRequest.SampleRequestId,
                CreatedDate = x.Row.SampleRequest.CreatedDate,
                CustomerId = x.Row.SampleRequest.CustomerId,
                Status = x.Row.SampleRequest.Status,
                HasFeedback = x.Row.HasPriceFeedback || x.Row.Trial != null && x.Row.Trial.CustomerReplyDate != null,
                SaleWritable = access.Sale && access.SaleOwners.Contains(x.Row.SampleRequest.ManagerBy) &&
                    writable.Any(w => w.SampleRequestId == x.Row.SampleRequest.SampleRequestId),
                Due = x.Facts.Quotation && !x.Facts.RequestedKg ? x.Row.SampleRequest.ExpectedPriceQuoteDate :
                    x.Row.SampleRequest.ExpectedDeliveryDate ?? x.Row.SampleRequest.RequestDeliveryDate ??
                    (x.Row.SampleRequest.LabReceivedDate.HasValue && x.Row.SampleRequest.NumberDeliverySampleDate >= 0 &&
                     x.Row.SampleRequest.NumberDeliverySampleDate <= (DateTime.MaxValue.Date - x.Row.SampleRequest.LabReceivedDate.Value.Date).TotalDays
                        ? (DateTime?)x.Row.SampleRequest.LabReceivedDate.Value.Date.AddDays(x.Row.SampleRequest.NumberDeliverySampleDate.GetValueOrDefault()) : null),
                State = new SampleRequestDailyPayloadPage.State(x.Row.SampleRequest.SampleRequestId,
                    x.Row.Trial == null ? null : x.Row.Trial.SampleRequestSampleTrialId, x.Facts,
                    ((x.Row.SampleRequest.CreatedDate < SampleFlowRules.StartsAt && !x.Row.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.Row.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? x.Row.SampleRequest.FormulaId :
                        x.Row.Trial == null ? null : x.Row.Trial.FormulaId) ?? x.Row.ConfirmedFormulaId ?? x.Row.SampleRequest.FormulaId,
                    x.Row.LabFormulaConfirmedAt,
                    x.Facts.Open && (x.Facts.Quotation && !x.Facts.RequestedKg ?
                        x.Row.SampleRequest.ExpectedPriceQuoteDate == null : x.Row.SampleRequest.ExpectedDeliveryDate == null))
            });

    internal static async Task<Result> ReadAsync(IQueryable<SampleRequestSampleTrialReportRow> filtered,
        IQueryable<SampleRequestSampleTrialReportRow> raw, IPLMDbContext db, ICRMReadDbContext crm, string currency,
        Access access, IQueryable<SampleRequest> writable, DateTime day, int pageNumber, int pageSize, CancellationToken ct)
        => await FromRowsAsync(ReadRows(filtered, db, crm, currency, access, writable)
            .TagWith("SampleDaily.PriorityFactsOnce").AsAsyncEnumerable(), raw, access, day, pageNumber, pageSize, ct);

    internal static async Task<Result> FromRowsAsync(IAsyncEnumerable<ReadRow> rows,
        IQueryable<SampleRequestSampleTrialReportRow> raw, Access access, DateTime day,
        int pageNumber, int pageSize, CancellationToken ct)
    {
        // Contiguous PostgreSQL customer order preserves collation/null/UUID ties. Keep only the requested prefix of groups.
        var prefixSize = Math.Min((long)pageNumber * pageSize, int.MaxValue);
        var pinned = (access.PinnedRequestIds ?? []).ToHashSet();
        var priorities = SampleWorkTaskPriority.Normalize(access.PriorityTasks).ToHashSet();
        var taskCounts = SampleWorkTaskPriority.Codes.ToDictionary(code => code, _ => 0);
        var retained = new List<CustomerGroup>();
        List<Candidate> current = [];
        Guid? customer = null;
        var tieOrder = 0;
        void Finish()
        {
            if (customer is not { } id || current.Count == 0) return;
            current.Sort(CompareRows);
            retained.Add(new(id, tieOrder++, current, current[0]));
            retained.Sort((a, b) => Compare(a.Best, b.Best) is var rank && rank != 0 ? rank :
                access.GroupByCustomer ? a.TieOrder.CompareTo(b.TieOrder) : a.Best.Row.State.RequestId.CompareTo(b.Best.Row.State.RequestId));
            if (retained.Count > prefixSize) retained.RemoveAt(retained.Count - 1);
            current = [];
        }
        await foreach (var row in rows.WithCancellation(ct))
        {
            ct.ThrowIfCancellationRequested();
            var groupId = access.GroupByCustomer ? row.CustomerId : row.State.RequestId;
            if (customer != groupId) { Finish(); customer = groupId; }
            var decision = SampleWorkDecisionRules.Resolve(row.State.Facts);
            var role = decision.Role;
            if (!MatchesBucket(row, decision, access.Bucket, day)) continue;
            // This is a work-category predicate, not visibility. Company/customer/ownership,
            // team and keyword filters already ran in SQL. Filter BEFORE count and paging.
            var salePending = SampleWorkDecisionRules.HasSalePriceWork(row.State.Facts) || SampleWorkDecisionRules.HasSaleFeedbackWork(row.State.Facts);
            if (access.RequiredRole is { } required && (!row.State.Facts.Open || role != required &&
                !(required == "Sale" && salePending) && !(required == "LabAdmin" && role == "Lab" && row.State.NeedsSchedule))) continue;
            var mine = access.Audience == SampleRequestWorkAudience.LabAdmin && access.Schedule && !row.State.Facts.LabPaused ||
                role == "Lab" && access.Technical && access.Audience != SampleRequestWorkAudience.Sale ||
                (role == "Sale" || salePending) && access.Sale && (row.SaleWritable || decision.Code == "ResumeAfterLabRejection") && access.Audience is not (SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin) ||
                role == "President" && access.Pricing || role == "LabAdmin" && access.Schedule &&
                access.Audience is null or SampleRequestWorkAudience.LabAdmin;
            var tasks = SampleWorkTaskPriority.Tasks(row.State.Facts, decision, access.Audience, row.State.NeedsSchedule, row.Status);
            foreach (var task in tasks) taskCounts[task]++;
            current.Add(new(row, SampleRequestWorkSortQuery.Rank(row.Status, mine, access.Audience,
                access.Audience == SampleRequestWorkAudience.Sale && SampleWorkDecisionRules.HasSaleFeedbackWork(row.State.Facts) && decision.Code != "CompleteRequest" ? "FollowUpCustomer" : decision.Code),
                row.State.Facts.Open && role == "Lab" && row.Due < day, pinned.Contains(row.State.RequestId), tasks.Any(priorities.Contains)));
        }
        Finish();
        var offset = (long)(pageNumber - 1) * pageSize;
        var selected = offset >= retained.Count ? [] : retained.Skip((int)offset).Take(pageSize).ToArray();
        return new(selected.Select(x => x.Id).ToArray(),
            SampleRequestDailyPayloadPage.FromStates(selected.SelectMany(x => x.Rows).Select(x => x.Row.State).ToArray(), raw), tieOrder)
        { TaskCounts = access.Bucket is SampleRequestWorkBucket.MyActionRequired or SampleRequestWorkBucket.ActionRequired ? taskCounts : null };
    }

    private static int CompareRows(Candidate a, Candidate b)
        => Compare(a, b) is var rank && rank != 0 ? rank : a.Row.State.RequestId.CompareTo(b.Row.State.RequestId);

    internal static IReadOnlyList<SampleRequestDailyPayloadPage.State> OrderPageStates(IEnumerable<ReadRow> rows, Access access, DateTime day)
    {
        var candidates = rows.Select(row =>
        {
            var decision = SampleWorkDecisionRules.Resolve(row.State.Facts);
            var role = decision.Role;
            var salePending = SampleWorkDecisionRules.HasSalePriceWork(row.State.Facts) || SampleWorkDecisionRules.HasSaleFeedbackWork(row.State.Facts);
            var mine = access.Audience == SampleRequestWorkAudience.LabAdmin && access.Schedule && !row.State.Facts.LabPaused ||
                role == "Lab" && access.Technical && access.Audience != SampleRequestWorkAudience.Sale ||
                (role == "Sale" || salePending) && access.Sale && (row.SaleWritable || decision.Code == "ResumeAfterLabRejection") && access.Audience is not (SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin) ||
                role == "President" && access.Pricing || role == "LabAdmin" && access.Schedule &&
                access.Audience is null or SampleRequestWorkAudience.LabAdmin;
            return new Candidate(row, SampleRequestWorkSortQuery.Rank(row.Status, mine, access.Audience,
                access.Audience == SampleRequestWorkAudience.Sale && SampleWorkDecisionRules.HasSaleFeedbackWork(row.State.Facts) && decision.Code != "CompleteRequest" ? "FollowUpCustomer" : decision.Code),
                row.State.Facts.Open && role == "Lab" && row.Due < day, false,
                SampleWorkTaskPriority.Tasks(row.State.Facts, decision, access.Audience, row.State.NeedsSchedule, row.Status)
                    .Any(task => access.PriorityTasks?.Contains(task) == true));
        }).ToList();
        candidates.Sort(CompareRows);
        return candidates.Select(x => x.Row.State).ToArray();
    }

    internal static bool MatchesBucket(ReadRow row, SampleWorkDecision decision, SampleRequestWorkBucket bucket, DateTime day)
        => bucket switch
        {
            SampleRequestWorkBucket.Completed => row.Status == "Completed",
            SampleRequestWorkBucket.Cancelled => row.Status == "Cancelled",
            SampleRequestWorkBucket.LabDeclined => row.Status == "LabDeclined",
            SampleRequestWorkBucket.FeedbackReceived => row.HasFeedback,
            SampleRequestWorkBucket.Rework => row.State.Facts.Open && decision.Code == "ReworkFormula",
            SampleRequestWorkBucket.NewRequestRequired => row.State.Facts.Open && decision.Code == "CreateNewRequest",
            SampleRequestWorkBucket.WaitingLab or SampleRequestWorkBucket.LabProgress => row.State.Facts.Open && decision.Role == "Lab",
            SampleRequestWorkBucket.WaitingSale => row.State.Facts.Open && (decision.Role == "Sale" || SampleWorkDecisionRules.HasSalePriceWork(row.State.Facts) || SampleWorkDecisionRules.HasSaleFeedbackWork(row.State.Facts)),
            SampleRequestWorkBucket.WaitingPrice => row.State.Facts.Open && decision.Role == "President",
            SampleRequestWorkBucket.Overdue => row.State.Facts.Open && decision.Role == "Lab" && row.Due < day,
            _ => true
        };

    private static int Compare(Candidate a, Candidate b)
    {
        var pin = b.Pinned.CompareTo(a.Pinned);
        if (pin != 0) return pin;
        var preferred = b.Preferred.CompareTo(a.Preferred);
        if (preferred != 0) return preferred;
        var rank = a.Rank.CompareTo(b.Rank);
        if (rank != 0) return rank;
        var overdue = b.Overdue.CompareTo(a.Overdue);
        if (overdue != 0) return overdue;
        if (a.Row.Due == b.Row.Due) return 0;
        if (a.Row.Due is null) return 1;
        if (b.Row.Due is null) return -1;
        return a.Row.Due.Value.CompareTo(b.Row.Due.Value);
    }
}
