using System.Diagnostics;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleLabActionPruningPostgresTests(IsolatedGroupStoragePostgres postgres, ITestOutputHelper output)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task EarlyFilterPreservesScopedFacetsAndCustomerPages()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var type in new[] { typeof(Customer), typeof(Product), typeof(SampleRequest), typeof(SampleRequestSampleTrial),
                     typeof(Formula), typeof(ProductPricingVersion), typeof(SampleRequestWorkflowEvent) })
            await CreateTable(connection, db.Model.FindEntityType(type)!);
        var company = Guid.NewGuid(); var owner = Guid.NewGuid(); var product = Guid.NewGuid(); var formula = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Product))!, new()
        { [nameof(Product.ProductId)] = product, [nameof(Product.CompanyId)] = company, [nameof(Product.IsActive)] = true });
        await Insert(connection, db.Model.FindEntityType(typeof(Formula))!, new()
        { [nameof(Formula.FormulaId)] = formula, [nameof(Formula.CompanyId)] = company, [nameof(Formula.ProductId)] = product,
          [nameof(Formula.Status)] = "Approved", [nameof(Formula.IsActive)] = true });
        var accepted = new List<Guid>();
        for (var c = 0; c < 60; c++)
        {
            var customer = Guid.NewGuid();
            await Insert(connection, db.Model.FindEntityType(typeof(Customer))!, new()
            { [nameof(Customer.CustomerId)] = customer, [nameof(Customer.CompanyId)] = company,
              [nameof(Customer.ExternalId)] = $"KH_{c:D3}", [nameof(Customer.IsActive)] = true });
            for (var i = 0; i < 12; i++)
            {
                var id = Guid.NewGuid();
                if (i == 1) accepted.Add(id);
                await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
                {
                    [nameof(SampleRequest.SampleRequestId)] = id, [nameof(SampleRequest.CompanyId)] = i == 10 ? Guid.NewGuid() : company,
                    [nameof(SampleRequest.CustomerId)] = customer, [nameof(SampleRequest.ManagerBy)] = i == 11 ? Guid.NewGuid() : owner,
                    [nameof(SampleRequest.ProductId)] = product, [nameof(SampleRequest.FormulaId)] = i == 0 ? null : formula,
                    [nameof(SampleRequest.InfoType)] = "quotation", [nameof(SampleRequest.SampleQuantity)] = 1d,
                    [nameof(SampleRequest.IsActive)] = true, [nameof(SampleRequest.Status)] = i == 9 ? "Completed" : "InProgress",
                    [nameof(SampleRequest.CreatedDate)] = new DateTime(2026, 11, 2),
                    [nameof(SampleRequest.ExpectedDeliveryDate)] = new DateTime(2026, 11, 28),
                    [nameof(SampleRequest.ExternalId)] = $"TP_{c:D3}_{i:D2}"
                });
            }
        }
        var acceptedIds = accepted.ToArray();
        var scoped = db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == company && x.ManagerBy == owner && x.IsActive);
        var source = from x in scoped
                     join t in db.SampleRequestSampleTrials on x.SampleRequestId equals t.SampleRequestId into trials
                     from trial in trials.DefaultIfEmpty()
                     select new SampleRequestSampleTrialReportRow
                     { SampleRequest = x, Trial = trial, ConfirmedFormulaId = x.FormulaId,
                       LabFormulaConfirmedAt = x.LabReceivedDate, HasPriceFeedback = false,
                       HasFinalizedChip = false, HasAcceptedCustomerPrice = acceptedIds.Contains(x.SampleRequestId) };
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Lab, true, false, false, false, [],
            "Lab", SampleRequestWorkBucket.ActionRequired, PriorityTasks: ["SendSample"]);
        var baseline = SampleRequestDailyPriorityFactsPage.ReadRows(source, db, db, "VND", access with { Bucket = SampleRequestWorkBucket.All }, scoped);
        var optimized = SampleRequestDailyPriorityFactsPage.ReadRows(source, db, db, "VND", access, scoped);
        Assert.Equal(600, await baseline.CountAsync());
        Assert.Equal(120, await optimized.CountAsync());
        // Warm both shapes before measuring. Compare the full selection stage, not Count SQL.
        await Select(baseline, 1); await Select(optimized, 1);
        var beforeMs = new List<double>(); var afterMs = new List<double>();
        for (var run = 0; run < 3; run++)
        {
            var watch = Stopwatch.StartNew(); var before = await Select(baseline, run + 1);
            beforeMs.Add(watch.Elapsed.TotalMilliseconds);
            watch.Restart(); var after = await Select(optimized, run + 1);
            afterMs.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(60, after.TotalCustomers);
            Assert.Equal(before.TotalCustomers, after.TotalCustomers);
            Assert.Equal(before.CustomerIds, after.CustomerIds);
            Assert.Equal(before.Payload.States.Select(x => x.RequestId), after.Payload.States.Select(x => x.RequestId));
            Assert.Equal(before.TaskCounts!.OrderBy(x => x.Key), after.TaskCounts!.OrderBy(x => x.Key));
        }
        var measurement = $"Isolated PostgreSQL, 720 seeded / 600 scoped TP, 60 customers. Facts rows 600 -> 120. " +
            $"Selection warm median {beforeMs.Order().ElementAt(1):F2} -> {afterMs.Order().ElementAt(1):F2} ms. Not production/HTTP latency.";
        output.WriteLine(measurement);
        await File.WriteAllTextAsync(Path.Combine(postgres.RepositoryRoot, "artifacts", "lab-early-filter-measurements.txt"), measurement);

        Task<SampleRequestDailyPriorityFactsPage.Result> Select(IQueryable<SampleRequestDailyPriorityFactsPage.ReadRow> rows, int page)
            => SampleRequestDailyPriorityFactsPage.FromRowsAsync(rows.AsAsyncEnumerable(), source, access,
                new(2026, 11, 27), page, 15, CancellationToken.None);
    }
}
