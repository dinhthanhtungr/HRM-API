using System.Diagnostics;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleCustomerCodePagePostgresTests(IsolatedGroupStoragePostgres postgres, ITestOutputHelper output)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ReadsOnlySelectedCustomerTrialKeysAndPreservesLabPriority()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var tables = new[] { "Customer", "Product", "SampleRequest", "SampleRequestSampleTrial", "Formula", "FormulaVersion",
            "ProductPricingVersion", "SampleRequestWorkflowEvent", "InternalMessage", "InternalConversation", "Employee" };
        foreach (var type in db.Model.GetEntityTypes().Where(x => tables.Contains(x.ClrType.Name))) await CreateTable(connection, type);
        var company = Guid.NewGuid(); var customer = Guid.NewGuid(); var product = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Customer))!, new()
        {
            [nameof(Customer.CustomerId)] = customer, [nameof(Customer.CompanyId)] = company,
            [nameof(Customer.CustomerName)] = "Scoped customer", [nameof(Customer.ExternalId)] = "KH_TEST"
        });
        var requestModel = db.Model.FindEntityType(typeof(SampleRequest))!;
        var trialModel = db.Model.FindEntityType(typeof(SampleRequestSampleTrial))!;
        var expected = new List<(Guid Request, Guid? Trial)>();
        var foreign = Guid.NewGuid();
        for (var i = 0; i < 101; i++)
        {
            var id = Guid.NewGuid();
            await Insert(connection, requestModel, new()
            {
                [nameof(SampleRequest.SampleRequestId)] = id,
                [nameof(SampleRequest.CompanyId)] = i == 100 ? foreign : company,
                [nameof(SampleRequest.CustomerId)] = customer, [nameof(SampleRequest.ProductId)] = product,
                [nameof(SampleRequest.CreatedDate)] = new DateTime(2026, 11, 2),
                [nameof(SampleRequest.IsActive)] = true, [nameof(SampleRequest.InfoType)] = "Thẻ màu (1)",
                [nameof(SampleRequest.Status)] = "InProgress", [nameof(SampleRequest.SampleQuantity)] = 2m
            });
            Guid? trial = null;
            if (i % 2 == 0)
            {
                trial = Guid.NewGuid();
                await Insert(connection, trialModel, new()
                {
                    [nameof(SampleRequestSampleTrial.SampleRequestSampleTrialId)] = trial.Value,
                    [nameof(SampleRequestSampleTrial.SampleRequestId)] = id, [nameof(SampleRequestSampleTrial.TrialNo)] = 1,
                    [nameof(SampleRequestSampleTrial.Status)] = SampleTrialStatus.SampleSent,
                    [nameof(SampleRequestSampleTrial.IsActive)] = true, [nameof(SampleRequestSampleTrial.DeliveredSampleQuantityKg)] = 2m
                });
            }
            if (i != 100) expected.Add((id, trial));
        }
        var raw = from request in db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && x.CustomerId == customer)
                  join trial in db.SampleRequestSampleTrials.Where(x => x.IsActive) on request.SampleRequestId equals trial.SampleRequestId into trials
                  from trial in trials.DefaultIfEmpty()
                  select new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = trial };
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Lab, true, false, false, false, []);
        var stages = new List<string>();
        var watch = Stopwatch.StartNew();
        var selected = SampleRequestCustomerPageQuery.ExpandSelectedCustomers(raw, [customer], priorityAlreadyComputed: false);
        var page = await SampleRequestDailyPayloadPage.ReadCustomerPageAsync(raw, selected, db, db, "VND", access,
            db.SampleRequests.Where(_ => false), new(2026, 11, 9), default, stages.Add);
        watch.Stop();
        Assert.Equal(100, page.States.Count);
        Assert.Equal(expected.OrderBy(x => x.Request), page.States.Select(x => (x.RequestId, x.TrialId)).OrderBy(x => x.RequestId));
        // Missing VU requires Lab preparation, while sent trials wait for Sale: Lab work first.
        Assert.All(page.States.Take(50), state => Assert.Null(state.TrialId));
        Assert.All(page.States.Skip(50), state => Assert.NotNull(state.TrialId));
        Assert.Equal(new[] { "page-keys" }, stages);
        output.WriteLine($"CustomerCode keys + facts for 100 scoped requests: {watch.Elapsed.TotalMilliseconds:F2} ms");
        var newestWatch = Stopwatch.StartNew();
        var newest = await SampleRequestNewestFactsPage.ReadAsync(SampleRequestLabWorkQuery.Enrich(raw, db), raw, db, db, "VND", [], 1, 5, default);
        newestWatch.Stop();
        Assert.Equal(50, newest.TotalCount);
        Assert.Equal(5, newest.Payload.States.Count);
        Assert.All(newest.Payload.States, state => Assert.Null(state.TrialId));
        Assert.Equal(expected.Where(x => x.Trial == null).OrderByDescending(x => x.Request).Take(5).Select(x => x.Request),
            newest.Payload.States.Select(x => x.RequestId));
        output.WriteLine($"Newest actionable count + selected facts for 100 scoped requests: {newestWatch.Elapsed.TotalMilliseconds:F2} ms");
        var pinnedId = expected.Where(x => x.Trial == null).OrderBy(x => x.Request).First().Request;
        var withPin = await SampleRequestNewestFactsPage.ReadAsync(SampleRequestLabWorkQuery.Enrich(raw, db), raw, db, db,
            "VND", [pinnedId], 1, 5, default);
        Assert.Equal(50, withPin.TotalCount);
        Assert.Equal(pinnedId, withPin.Payload.States[0].RequestId);
        var secondPage = await SampleRequestNewestFactsPage.ReadAsync(SampleRequestLabWorkQuery.Enrich(raw, db), raw, db, db,
            "VND", [pinnedId], 2, 5, default);
        Assert.Equal(50, secondPage.TotalCount);
        Assert.Empty(withPin.Payload.States.Select(x => x.RequestId).Intersect(secondPage.Payload.States.Select(x => x.RequestId)));
    }
}
