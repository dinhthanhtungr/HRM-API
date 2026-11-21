using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

/// <summary>Pages customer IDs from an already scoped/filtered query, then expands only those matching groups.</summary>
internal static class SampleRequestCustomerPageQuery
{
    internal static IQueryable<SampleRequestSampleTrialReportRow> ExpandSelectedCustomers(
        IQueryable<SampleRequestSampleTrialReportRow> rows, Guid[] customerIds, bool priorityAlreadyComputed)
    {
        var selected = rows.Where(x => customerIds.Contains(x.SampleRequest.CustomerId));
        // The lean CustomerCode path has no SQL rank projection. Its bounded facts
        // are ranked after materialization by ReadCustomerPageAsync/OrderPageStates.
        return priorityAlreadyComputed ? SampleRequestWorkSortQuery.OrderPriority(selected)
            : selected.OrderBy(x => x.SampleRequest.SampleRequestId);
    }

    internal static IQueryable<SampleRequestSampleTrialReportRow> Apply(
        IQueryable<SampleRequestSampleTrialReportRow> filtered, int pageNumber, int pageSize)
    {
        var customers = filtered.Select(x => new
            {
                x.SampleRequest.CustomerId,
                x.SampleRequest.Customer.CustomerName
            })
            .Distinct()
            .OrderBy(x => x.CustomerName).ThenBy(x => x.CustomerId)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => x.CustomerId);

        return filtered.Where(x => customers.Contains(x.SampleRequest.CustomerId))
            .OrderBy(x => x.SampleRequest.Customer.CustomerName)
            .ThenBy(x => x.SampleRequest.CustomerId)
            .ThenByDescending(x => x.SampleRequest.CreatedDate)
            .ThenBy(x => x.SampleRequest.ExternalId)
            .ThenBy(x => x.SampleRequest.SampleRequestId);
    }

    internal static IQueryable<Guid> SelectCodePageCustomerIds(
        IQueryable<SampleRequestSampleTrialReportRow> rows, int pageNumber, int pageSize, Guid[]? pinnedRequestIds = null)
    {
        var pins = pinnedRequestIds ?? [];
        var customers = rows.GroupBy(x => new { x.SampleRequest.CustomerId, Code = x.SampleRequest.Customer.ExternalId })
            .Select(g => new { g.Key.CustomerId, g.Key.Code, Pinned = g.Any(x => pins.Contains(x.SampleRequest.SampleRequestId)) });
        return customers.OrderByDescending(x => x.Pinned).ThenBy(x => x.Code == null || x.Code == "").ThenBy(x => x.Code).ThenBy(x => x.CustomerId)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(x => x.CustomerId);
    }

    /// <summary>One narrow scoped scan; retain one tuple per customer rather than all TP payloads.</summary>
    internal static async Task<Guid[]> SelectPriorityCustomerIdsAsync(
        IQueryable<SampleRequestSampleTrialReportRow> rows, int pageNumber, int pageSize, CancellationToken ct, Guid[]? pinnedRequestIds = null)
    {
        var page = new SampleCustomerPriorityPage();
        await foreach (var fact in PriorityFacts(rows, pinnedRequestIds).TagWith("SampleDaily.CustomerPriority").AsAsyncEnumerable().WithCancellation(ct)) page.Include(fact);
        return page.GetIds(pageNumber, pageSize);
    }

    internal static IQueryable<SampleCustomerPriorityFact> PriorityFacts(IQueryable<SampleRequestSampleTrialReportRow> rows, Guid[]? pinnedRequestIds = null)
        // Keep PostgreSQL code/null/collation and UUID tie ordering without sorting expensive CASE expressions.
        => rows.OrderBy(x => x.SampleRequest.Customer.ExternalId).ThenBy(x => x.SampleRequest.CustomerId)
            .Select(x => new SampleCustomerPriorityFact
            {
                CustomerId = x.SampleRequest.CustomerId, Code = x.SampleRequest.Customer.ExternalId,
                Pinned = (pinnedRequestIds ?? Array.Empty<Guid>()).Contains(x.SampleRequest.SampleRequestId),
                Rank = x.WorkSortRank, Overdue = x.WorkSortOverdue, Due = x.WorkSortDue
            });
}
