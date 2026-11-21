using System.Text.Json;
using System.Diagnostics;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleCustomerPriorityPerformanceTests(IsolatedGroupStoragePostgres postgres, ITestOutputHelper output)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task GroupPriorityStreamsCanonicalFactsWithoutPerCustomerLeaderSubqueries()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection, options => options.CommandTimeout(120)).Options);
        foreach (var type in new[] { typeof(Customer), typeof(Product), typeof(SampleRequest), typeof(SampleRequestSampleTrial),
                     typeof(Formula), typeof(ProductPricingVersion), typeof(SampleRequestWorkflowEvent) })
            await CreateTable(connection, db.Model.FindEntityType(type)!);
        var company = Guid.NewGuid(); var owner = Guid.NewGuid();
        var customerEntity = db.Model.FindEntityType(typeof(Customer))!;
        var requestEntity = db.Model.FindEntityType(typeof(SampleRequest))!;
        for (var customerIndex = 0; customerIndex < 300; customerIndex++)
        {
            var customer = Guid.NewGuid();
            await Insert(connection, customerEntity, new()
            {
                [nameof(Customer.CustomerId)] = customer, [nameof(Customer.CompanyId)] = company,
                [nameof(Customer.CustomerName)] = "Customer " + customerIndex.ToString("D4"),
                [nameof(Customer.ExternalId)] = "KH_" + customerIndex.ToString("D4")
            });
            for (var requestIndex = 0; requestIndex < 20; requestIndex++)
                await Insert(connection, requestEntity, new()
                {
                    [nameof(SampleRequest.SampleRequestId)] = Guid.NewGuid(), [nameof(SampleRequest.CompanyId)] = company,
                    [nameof(SampleRequest.CustomerId)] = customer, [nameof(SampleRequest.ManagerBy)] = owner,
                    [nameof(SampleRequest.IsActive)] = true, [nameof(SampleRequest.Status)] = "InProgress",
                    [nameof(SampleRequest.CreatedDate)] = new DateTime(2026, 11, 2),
                    [nameof(SampleRequest.ExternalId)] = $"TP_{customerIndex:D4}_{requestIndex:D2}",
                    [nameof(SampleRequest.ExpectedDeliveryDate)] = new DateTime(2026, 11, 1).AddDays((customerIndex + requestIndex) % 20)
                });
        }
        var source = from request in db.SampleRequests.AsNoTracking()
                     where request.CompanyId == company && request.ManagerBy == owner && request.IsActive
                     join candidate in db.SampleRequestSampleTrials on request.SampleRequestId equals candidate.SampleRequestId into trials
                     from trial in trials.DefaultIfEmpty()
                     select new SampleRequestSampleTrialReportRow
                     {
                         SampleRequest = request, Trial = trial, ConfirmedFormulaId = request.FormulaId,
                         LabFormulaConfirmedAt = request.LabReceivedDate, HasAcceptedCustomerPrice = false,
                         HasFinalizedChip = false, HasPriceFeedback = false, TrialCount = 0
                     };
        var canonical = SampleWorkDecisionQuery.Enrich(source, db, db, "VND");
        var ranked = SampleRequestWorkSortQuery.Enrich(canonical, SampleRequestWorkAudience.Sale,
            new DateTime(2026, 11, 6), false, true, false, false, [owner], db.SampleRequests.Where(x => x.CompanyId == company && x.ManagerBy == owner));
        var before = PreviousCustomerPage(ranked);
        var after = SampleRequestCustomerPageQuery.PriorityFacts(ranked);
        var afterPlan = await Explain(after);
        // Use a materialized reference sort so the test does not execute the retired
        // correlated query, whose repeated decision expressions can exhaust PostgreSQL memory.
        var facts = await ranked.Select(x => new
        {
            x.SampleRequest.CustomerId, Code = x.SampleRequest.Customer.ExternalId,
            x.WorkSortRank, x.WorkSortOverdue, x.WorkSortDue
        }).ToArrayAsync();
        Assert.Equal(6000, facts.Length);
        var expected = facts.OrderBy(x => x.WorkSortRank).ThenByDescending(x => x.WorkSortOverdue)
            .ThenBy(x => x.WorkSortDue is null).ThenBy(x => x.WorkSortDue)
            .ThenBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.CustomerId)
            .Select(x => x.CustomerId).Distinct().Take(15).ToArray();
        var timer = Stopwatch.StartNew();
        var actual = await SampleRequestCustomerPageQuery.SelectPriorityCustomerIdsAsync(ranked, 1, 15, CancellationToken.None);
        timer.Stop(); var afterEndToEndMs = timer.Elapsed.TotalMilliseconds;
        Assert.Equal(expected, actual);
        // One narrow projection replaces the per-customer best-work searches.
        Assert.True(after.ToQueryString().Length < before.ToQueryString().Length);
        output.WriteLine($"6000 TP / 300 customers; canonical facts execution {afterPlan.ExecutionMs:F2} ms, planning {afterPlan.PlanningMs:F2} ms; SQL length {before.ToQueryString().Length}/{after.ToQueryString().Length}; end-to-end {afterEndToEndMs:F2} ms");
        var artifact = Path.Combine(postgres.RepositoryRoot, "artifacts", "customer-priority-performance");
        Directory.CreateDirectory(artifact);
        await File.WriteAllTextAsync(Path.Combine(artifact, "after-plan.json"), afterPlan.Plan);
        await File.WriteAllTextAsync(Path.Combine(artifact, "measurements.txt"),
            $"6000 TP / 300 customers. Canonical facts execution={afterPlan.ExecutionMs:F2}ms planning={afterPlan.PlanningMs:F2}ms. SQL length before={before.ToQueryString().Length} after={after.ToQueryString().Length}. End-to-end={afterEndToEndMs:F2}ms.");

        async Task<(string Plan, double ExecutionMs, double PlanningMs)> Explain<T>(IQueryable<T> query)
        {
            await using var command = query.CreateDbCommand();
            command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + command.CommandText;
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            var plan = reader.GetString(0);
            using var json = JsonDocument.Parse(plan);
            return (plan, json.RootElement[0].GetProperty("Execution Time").GetDouble(),
                json.RootElement[0].GetProperty("Planning Time").GetDouble());
        }
    }

    private static IQueryable<Guid> PreviousCustomerPage(IQueryable<SampleRequestSampleTrialReportRow> rows)
    {
        var customers = rows.Select(x => new { x.SampleRequest.CustomerId, Code = x.SampleRequest.Customer.ExternalId }).Distinct();
        var leaders = customers.Select(customer => new
        {
            customer.CustomerId, customer.Code,
            Rank = rows.Where(x => x.SampleRequest.CustomerId == customer.CustomerId)
                .OrderBy(x => x.WorkSortRank).ThenByDescending(x => x.WorkSortOverdue)
                .ThenBy(x => x.WorkSortDue == null).ThenBy(x => x.WorkSortDue).ThenBy(x => x.SampleRequest.SampleRequestId)
                .Select(x => x.WorkSortRank).FirstOrDefault(),
            Overdue = rows.Where(x => x.SampleRequest.CustomerId == customer.CustomerId)
                .OrderBy(x => x.WorkSortRank).ThenByDescending(x => x.WorkSortOverdue)
                .ThenBy(x => x.WorkSortDue == null).ThenBy(x => x.WorkSortDue).ThenBy(x => x.SampleRequest.SampleRequestId)
                .Select(x => x.WorkSortOverdue).FirstOrDefault(),
            Due = rows.Where(x => x.SampleRequest.CustomerId == customer.CustomerId)
                .OrderBy(x => x.WorkSortRank).ThenByDescending(x => x.WorkSortOverdue)
                .ThenBy(x => x.WorkSortDue == null).ThenBy(x => x.WorkSortDue).ThenBy(x => x.SampleRequest.SampleRequestId)
                .Select(x => x.WorkSortDue).FirstOrDefault()
        });
        return leaders.OrderBy(x => x.Rank).ThenByDescending(x => x.Overdue)
            .ThenBy(x => x.Due == null).ThenBy(x => x.Due).ThenBy(x => x.Code).ThenBy(x => x.CustomerId)
            .Take(15).Select(x => x.CustomerId);
    }
}
