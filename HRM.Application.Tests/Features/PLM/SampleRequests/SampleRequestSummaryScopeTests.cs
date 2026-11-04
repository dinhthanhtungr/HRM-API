using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Tests.Features.PLM.ProductionOrders;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestSummaryScopeTests
{
    [Fact]
    public void SummaryAndDrilldownUseOperationalRequestsAndLatestLegacyChoice()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        SampleRequest Request(DateTime created) => new()
        {
            SampleRequestId = Guid.NewGuid(), CompanyId = company, CreatedDate = created,
            LabReceivedDate = new DateTime(2026, 11, 2), Status = "InProgress"
        };
        var current = Request(new DateTime(2026, 11, 1));
        var tracked = Request(new DateTime(2026, 10, 1));
        var excluded = Request(new DateTime(2026, 10, 2));
        excluded.IsDelayed = true;
        var wrongCompany = Request(new DateTime(2026, 10, 3));
        var events = new[] {
            new SampleRequestWorkflowEvent { CompanyId = company, SampleRequestId = tracked.SampleRequestId,
                EventType = SampleRequestWorkScope.LegacyIncluded, RecordedAt = new DateTime(2026, 11, 1) },
            new SampleRequestWorkflowEvent { CompanyId = Guid.NewGuid(), SampleRequestId = wrongCompany.SampleRequestId,
                EventType = SampleRequestWorkScope.LegacyIncluded, RecordedAt = new DateTime(2026, 11, 1) }
        };
        store.Seed(nameof(db.SampleRequestWorkflowEvents), events);
        store.Seed<SampleRequestSampleTrial>(nameof(db.SampleRequestSampleTrials));
        var source = new[] { current, tracked, excluded, wrongCompany }.AsQueryable();
        var records = SampleRequestAdminReportQuery.Build(source, db).ToList().AsQueryable();
        Assert.Equal(2, records.Count());
        var start = new DateTime(2026, 11, 1);
        var end = start.AddMonths(1);
        var details = SampleRequestAdminReportQuery.Filter(records, SampleRequestAdminMetric.Received, start, end).ToList();
        Assert.Equal(2, details.Count);
        Assert.Equal(details.Count, records.Count(x => x.ReceivedDate >= start && x.ReceivedDate < end));
        Assert.Equal(2, SampleRequestAdminReportQuery.Filter(records, SampleRequestAdminMetric.Open,
            new DateTime(2027, 1, 1), new DateTime(2027, 2, 1)).Count());
        var revoke = new SampleRequestWorkflowEvent { CompanyId = company,
            SampleRequestId = tracked.SampleRequestId, EventType = SampleRequestWorkScope.LegacyExcluded,
            RecordedAt = new DateTime(2026, 11, 3) };
        store.Seed(nameof(db.SampleRequestWorkflowEvents), events.Append(revoke).ToArray());
        Assert.Equal(current.SampleRequestId, Assert.Single(SampleRequestAdminReportQuery.Build(source, db)).SampleRequestId);
    }
}
