using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleAdminDetailsPagingTests
{
    [Theory]
    [InlineData(SampleRequestAdminMetric.CompletedQuotation)]
    [InlineData(SampleRequestAdminMetric.CompletedChip)]
    [InlineData(SampleRequestAdminMetric.CompletedSample)]
    [InlineData(SampleRequestAdminMetric.CompletedOther)]
    public void CompletionKindFiltersAndAggregationTranslateBeforePaging(SampleRequestAdminMetric metric)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var records = SampleRequestAdminReportQuery.Build(db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive), db, db.ProductPricingVersions);
        var filtered = SampleRequestAdminReportQuery.Filter(records, metric, new(2026, 11, 1), new(2026, 12, 1));
        var sql = GetSampleRequestAdminDetailsQueryHandler.SelectPageIds(filtered, 1, 15).ToQueryString();
        Assert.Contains("LIMIT", sql);
        Assert.Contains("CompanyId", sql);
        var grouped = filtered.GroupBy(x => new { x.BranchId, x.LabCompletedKind })
            .Select(g => new { g.Key.BranchId, g.Key.LabCompletedKind, Count = g.Count() }).ToQueryString();
        Assert.Contains("GROUP BY", grouped);
    }
    [Fact]
    public void PagesOnlyMatchingRequestsInStableCreatedOrder()
    {
        var items = Enumerable.Range(1, 6).Select(n => new SampleRequest
        { SampleRequestId = Guid.NewGuid(), ExternalId = $"TP_{n}", CreatedDate = new DateTime(2026, 11, n) }).ToArray();
        var records = items.Where((_, index) => index != 4).Select(x => new SampleRequestAdminReportRecord { SampleRequestId = x.SampleRequestId, CreatedDate = x.CreatedDate, ExternalId = x.ExternalId }).AsQueryable();
        var first = GetSampleRequestAdminDetailsQueryHandler.SelectPageIds(records, 1, 2).ToArray();
        var second = GetSampleRequestAdminDetailsQueryHandler.SelectPageIds(records, 2, 2).ToArray();
        Assert.Equal(new[] { items[5].SampleRequestId, items[3].SampleRequestId }, first);
        Assert.Equal(new[] { items[2].SampleRequestId, items[1].SampleRequestId }, second);
        Assert.Empty(first.Intersect(second));
    }

    [Fact]
    public void SelectsIdsWithLimitBeforeAnyWorkflowDetailProjection()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var source = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive);
        var rows = SampleRequestAdminReportQuery.Filter(SampleRequestAdminReportQuery.Build(source, db),
            SampleRequestAdminMetric.InProgress, new DateTime(2026, 11, 1), new DateTime(2026, 12, 1));
        var sql = GetSampleRequestAdminDetailsQueryHandler.SelectPageIds(rows, 2, 15).ToQueryString();
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.DoesNotContain("FormulaMaterials", sql);
        Assert.DoesNotContain("ProductPricingVersions", sql);
        Assert.DoesNotContain("TrialCount", sql);
    }
}
