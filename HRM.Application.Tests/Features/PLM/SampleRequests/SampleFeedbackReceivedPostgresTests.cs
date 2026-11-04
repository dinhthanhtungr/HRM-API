using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleFeedbackReceivedPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ActualPriceFeedbackIsFilteredInSqlBeforeCountAndPagingWithoutCrossCompanyEvents()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var type in new[] { typeof(SampleRequest), typeof(SampleRequestSampleTrial), typeof(Formula), typeof(SampleRequestWorkflowEvent), typeof(ProductPricingVersion) })
            await CreateTable(connection, db.Model.FindEntityType(type)!);
        var company = Guid.NewGuid(); var now = new DateTime(2026, 11, 4);
        var cases = new[]
        {
            ("01-accepted", SampleRequestWorkflowEvents.PriceAccepted, false, false, true),
            ("02-rejected", SampleFlowRules.PriceRejected, false, false, true),
            ("03-noresponse", SampleFlowRules.PriceNoResponse, false, false, true),
            ("04-revoked-only", SampleRequestWorkflowEvents.PriceRevoked, false, false, false),
            ("05-accepted-revoked", SampleRequestWorkflowEvents.PriceAccepted, false, true, true),
            ("06-quote-date-only", "", false, false, false),
            ("07-wrong-company", SampleFlowRules.PriceRejected, true, false, false),
            ("08-trial-feedback", "", false, false, true)
        };
        foreach (var (name, type, wrongCompany, revoked, included) in cases)
        {
            var id = Guid.NewGuid();
            await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
            { ["SampleRequestId"] = id, ["CompanyId"] = company, ["ExternalId"] = name, ["IsActive"] = true,
              ["CreatedDate"] = now, ["Status"] = "InProgress", ["RealPriceQuoteDate"] = now });
            if (type != "")
                await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestWorkflowEvent))!, new()
                { ["Id"] = Guid.NewGuid(), ["SampleRequestId"] = id, ["CompanyId"] = wrongCompany ? Guid.NewGuid() : company,
                  ["EventType"] = type, ["RecordedAt"] = now });
            if (revoked)
                await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestWorkflowEvent))!, new()
                { ["Id"] = Guid.NewGuid(), ["SampleRequestId"] = id, ["CompanyId"] = company,
                  ["EventType"] = SampleRequestWorkflowEvents.PriceRevoked, ["RecordedAt"] = now.AddHours(1) });
            if (name == "08-trial-feedback")
                await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestSampleTrial))!, new()
                { ["SampleRequestSampleTrialId"] = Guid.NewGuid(), ["SampleRequestId"] = id,
                  ["IsActive"] = true, ["CustomerReplyDate"] = now });
        }
        var rows = from request in db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive)
                   join trial in db.SampleRequestSampleTrials on request.SampleRequestId equals trial.SampleRequestId into trials
                   from trial in trials.DefaultIfEmpty()
                   select new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = trial,
                       ConfirmedFormulaId = request.FormulaId, LabFormulaConfirmedAt = request.LabReceivedDate };
        rows = SampleFlowQueueQuery.Enrich(rows, db);
        rows = SampleWorkDecisionQuery.Enrich(rows, db, db, "VND");
        var filtered = SampleRequestWorkQueueQuery.Apply(rows,
            new GetSampleRequestSampleTrialsQuery { WorkBucket = SampleRequestWorkBucket.FeedbackReceived }, db);
        var expected = cases.Where(x => x.Item5).Select(x => x.Item1).ToArray();
        Assert.Equal(expected.Length, await filtered.CountAsync());
        var page = filtered.OrderBy(x => x.SampleRequest.ExternalId).Skip(1).Take(3).Select(x => x.SampleRequest.ExternalId);
        Assert.Contains("LIMIT", page.ToQueryString()); Assert.Contains("EXISTS", page.ToQueryString());
        Assert.Equal(expected.Skip(1).Take(3), await page.ToArrayAsync());
    }
}
