using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

/// <summary>Lab actionable newest requests: one scoped facts stream for count and selected page.</summary>
internal static class SampleRequestNewestFactsPage
{
    internal static bool CanRead(GetSampleRequestSampleTrialsQuery request)
        => request.WorkSort == SampleRequestWorkSort.Newest && !request.GroupByCustomer &&
           !request.IncludeTrialHistory && request.WorkAudience == SampleRequestWorkAudience.Lab &&
           request.WorkBucket == SampleRequestWorkBucket.ActionRequired;

    internal sealed record Result(int TotalCount, SampleRequestDailyPayloadPage Payload);

    internal static Task<Result> ReadAsync(IQueryable<SampleRequestSampleTrialReportRow> filtered,
        IQueryable<SampleRequestSampleTrialReportRow> raw, IPLMDbContext db, ICRMReadDbContext crm, string currency,
        Guid[] pins, int pageNumber, int pageSize, CancellationToken ct)
        => FromRowsAsync(SampleRequestDailyPriorityFactsPage.ReadRows(filtered, db, crm, currency,
                new(SampleRequestWorkAudience.Lab, true, false, false, false, []), db.SampleRequests.Where(_ => false))
            .OrderByDescending(x => pins.Contains(x.RequestId)).ThenByDescending(x => x.CreatedDate).ThenByDescending(x => x.RequestId)
            .TagWith("SampleDaily.NewestFactsOnce").AsAsyncEnumerable(), raw, pageNumber, pageSize, ct);

    internal static async Task<Result> FromRowsAsync(IAsyncEnumerable<SampleRequestDailyPriorityFactsPage.ReadRow> rows,
        IQueryable<SampleRequestSampleTrialReportRow> raw, int pageNumber, int pageSize, CancellationToken ct)
    {
        var offset = (long)(pageNumber - 1) * pageSize;
        var selected = new List<SampleRequestDailyPayloadPage.State>(pageSize);
        var count = 0;
        await foreach (var row in rows.WithCancellation(ct))
        {
            if (!row.State.Facts.Open || SampleWorkDecisionRules.Resolve(row.State.Facts).Role != "Lab") continue;
            if (count >= offset && selected.Count < pageSize) selected.Add(row.State);
            count++;
        }
        return new(count, SampleRequestDailyPayloadPage.FromStates(selected, raw));
    }
}
