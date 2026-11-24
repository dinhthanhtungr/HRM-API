using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleCustomerCodePageTests
{
    [Fact]
    public void NewestSqlRetainsScopeAndOrdersBeforeLimitWithoutWorkflowRanking()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var rows = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive)
            .Select(x => new SampleRequestSampleTrialReportRow { SampleRequest = x });
        var sql = SampleRequestWorkSortQuery.OrderNewest(rows, [Guid.NewGuid()]).Skip(15).Take(15)
            .Select(x => x.SampleRequest.SampleRequestId).ToQueryString();
        Assert.Contains("CompanyId", sql);
        Assert.Contains("IsActive", sql);
        Assert.Contains("\"CreatedDate\" DESC", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.DoesNotContain("WorkSortRank", sql);
        Assert.DoesNotContain("ProductPricingVersions", sql);
    }
    [Fact]
    public void ExpandingLeanCustomerPageTranslatesWithoutUnprojectedPriorityColumns()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid(); var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var rows = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive)
            .Select(x => new SampleRequestSampleTrialReportRow { SampleRequest = x });
        var page = SampleRequestCustomerPageQuery.ExpandSelectedCustomers(rows, ids, false);
        var sql = SampleRequestDailyPayloadPage.CustomerPageKeys(page).ToQueryString();
        Assert.Contains("CompanyId", sql);
        Assert.Contains("CustomerId", sql);
        Assert.Contains("IsActive", sql);
        Assert.DoesNotContain("WorkSortRank", sql);
        Assert.DoesNotContain("WorkSortDue", sql);
        Assert.DoesNotContain("CASE", sql);
    }
    [Theory]
    [InlineData(SampleRequestWorkAudience.Lab)]
    [InlineData(SampleRequestWorkAudience.Sale)]
    [InlineData(SampleRequestWorkAudience.LabAdmin)]
    public void PageOrderMatchesExistingPriorityForEveryWorkflowCase(SampleRequestWorkAudience audience)
    {
        var day = new DateTime(2026, 11, 9); var owner = Guid.NewGuid();
        var rows = new List<SampleRequestSampleTrialReportRow>();
        var read = new List<SampleRequestDailyPriorityFactsPage.ReadRow>();
        var writable = new List<SampleRequest>();
        foreach (var input in SampleWorkDecisionTests.Cases())
        {
            var facts = (SampleWorkFacts)input[1]; var index = rows.Count;
            DateTime? due = index % 3 == 0 ? null : day.AddDays(index % 3 == 1 ? -2 : 2);
            var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), ManagerBy = index % 2 == 0 ? owner : Guid.NewGuid(),
                Status = facts.Open ? "InProgress" : index % 2 == 0 ? "Completed" : "Cancelled",
                InfoType = facts.Quotation ? "quotation" : "Thẻ màu", SampleQuantity = facts.RequestedKg ? 2 : 0,
                ExpectedDeliveryDate = due, ExpectedPriceQuoteDate = due };
            if (index % 3 != 0) writable.Add(request);
            var decision = SampleWorkDecisionRules.Resolve(facts);
            rows.Add(new() { SampleRequest = request, WorkDecision = decision,
                LabTaskRank = SampleWorkTaskPriority.ResolveLabRank(facts, decision.Code),
                HasSalePriceWork = SampleWorkDecisionRules.HasSalePriceWork(facts),
                HasSaleFeedbackWork = SampleWorkDecisionRules.HasSaleFeedbackWork(facts) });
            read.Add(new() { Status = request.Status, Due = due, SaleWritable = request.ManagerBy == owner && writable.Contains(request),
                State = new(request.SampleRequestId, null, facts, null, null, false) });
        }
        var ranked = SampleRequestWorkSortQuery.Enrich(rows.AsQueryable(), audience, day, true, true, true, true, [owner], writable.AsQueryable());
        var expected = SampleRequestWorkSortQuery.OrderPriority(ranked).Select(x => x.SampleRequest.SampleRequestId).ToArray();
        var actual = SampleRequestDailyPriorityFactsPage.OrderPageStates(read,
            new(audience, true, true, true, true, [owner]), day).Select(x => x.RequestId).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CustomerKeysRemoveWorkflowSortButRetainRequestAndTrialFilters()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid(); var customer = Guid.NewGuid();
        var raw = from request in db.SampleRequests.AsNoTracking()
                  where request.CompanyId == company && request.IsActive && request.CustomerId == customer
                  join trial in db.SampleRequestSampleTrials.Where(x => x.IsActive) on request.SampleRequestId equals trial.SampleRequestId into trials
                  from trial in trials.DefaultIfEmpty()
                  select new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = trial };
        var decisions = SampleWorkDecisionQuery.Enrich(SampleRequestLabWorkQuery.Enrich(raw, db), db, db, "VND");
        var selected = SampleRequestWorkSortQuery.OrderPriority(SampleRequestWorkSortQuery.Enrich(decisions,
            SampleRequestWorkAudience.Lab, new(2026, 11, 9), true, false, false, false, [], db.SampleRequests.Where(_ => false)));
        var oldSql = selected.Select(x => x.SampleRequest.SampleRequestId).ToQueryString();
        var sql = SampleRequestDailyPayloadPage.CustomerPageKeys(SampleRequestLabWorkQuery.Enrich(raw, db)).ToQueryString();
        Assert.Contains("CASE", oldSql);
        Assert.DoesNotContain("CASE", sql);
        Assert.DoesNotContain("ProductPricingVersions", sql);
        Assert.DoesNotContain("SampleRequestWorkflowEvents", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("CustomerId", sql);
        Assert.Contains("SampleRequestSampleTrialId", sql);
        Assert.Contains("IsActive", sql);
        Assert.DoesNotContain("ORDER BY", sql);
    }
}
