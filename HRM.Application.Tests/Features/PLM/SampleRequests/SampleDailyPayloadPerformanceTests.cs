using System.Diagnostics;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleDailyPayloadPerformanceTests(IsolatedGroupStoragePostgres postgres, ITestOutputHelper output)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task DailyPayloadReadsFactsOnceAndBatchesFormulaDetailsOnPopulatedTables()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection, x => x.CommandTimeout(120)).Options);
        var tables = new[] { "Customer", "Product", "SampleRequest", "SampleRequestSampleTrial", "Formula", "FormulaVersion",
            "ProductPricingVersion", "SampleRequestWorkflowEvent", "InternalMessage", "InternalConversation", "Employee" };
        foreach (var type in db.Model.GetEntityTypes().Where(x => tables.Contains(x.ClrType.Name)))
            await CreateTable(connection, type);
        var company = Guid.NewGuid(); var owner = Guid.NewGuid(); var product = Guid.NewGuid(); var customer = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Customer))!, new()
        {
            [nameof(Customer.CustomerId)] = customer, [nameof(Customer.CompanyId)] = company,
            [nameof(Customer.CustomerName)] = "Payload customer", [nameof(Customer.ExternalId)] = "KH_001"
        });
        await Insert(connection, db.Model.FindEntityType(typeof(Employee))!, new()
        {
            [nameof(Employee.EmployeeId)] = owner, [nameof(Employee.CompanyId)] = company,
            [nameof(Employee.FullName)] = "Fixture author"
        });
        var formulaEntity = db.Model.FindEntityType(typeof(Formula))!;
        var pricingEntity = db.Model.FindEntityType(typeof(ProductPricingVersion))!;
        var formulaIds = new List<Guid>();
        for (var i = 0; i < 30000; i++)
        {
            var formula = Guid.NewGuid();
            if (i < 90) formulaIds.Add(formula);
            await Insert(connection, formulaEntity, new()
            {
                [nameof(Formula.FormulaId)] = formula, [nameof(Formula.CompanyId)] = i == 1 ? Guid.NewGuid() : company,
                [nameof(Formula.ProductId)] = i < 90 && i != 3 ? product : Guid.NewGuid(), [nameof(Formula.IsActive)] = i != 2,
                [nameof(Formula.Status)] = i % 3 == 0 ? "Completed" : "Approved", [nameof(Formula.ExternalId)] = $"VU_{i}",
                [nameof(Formula.CreatedBy)] = owner
            });
            if (i < 4400)
                await Insert(connection, pricingEntity, new()
                {
                    [nameof(ProductPricingVersion.ProductPricingVersionId)] = Guid.NewGuid(),
                    [nameof(ProductPricingVersion.CompanyId)] = company, [nameof(ProductPricingVersion.ProductId)] = i < 90 ? product : Guid.NewGuid(),
                    [nameof(ProductPricingVersion.SourceFormulaId)] = formula, [nameof(ProductPricingVersion.IsActive)] = true,
                    [nameof(ProductPricingVersion.Status)] = ProductPricingStatus.Approved,
                    [nameof(ProductPricingVersion.Currency)] = "VND", [nameof(ProductPricingVersion.Version)] = i,
                    [nameof(ProductPricingVersion.StandardSellingPrice)] = 100m
                });
        }
        var outOfScope = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
        {
            [nameof(SampleRequest.SampleRequestId)] = outOfScope, [nameof(SampleRequest.CompanyId)] = Guid.NewGuid(),
            [nameof(SampleRequest.CustomerId)] = customer, [nameof(SampleRequest.ManagerBy)] = owner,
            [nameof(SampleRequest.ProductId)] = product, [nameof(SampleRequest.IsActive)] = true,
            [nameof(SampleRequest.Status)] = "InProgress", [nameof(SampleRequest.CreatedDate)] = new DateTime(2026, 11, 2)
        });
        var otherOwner = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
        {
            [nameof(SampleRequest.SampleRequestId)] = otherOwner, [nameof(SampleRequest.CompanyId)] = company,
            [nameof(SampleRequest.CustomerId)] = customer, [nameof(SampleRequest.ManagerBy)] = Guid.NewGuid(),
            [nameof(SampleRequest.ProductId)] = product, [nameof(SampleRequest.IsActive)] = true,
            [nameof(SampleRequest.Status)] = "InProgress", [nameof(SampleRequest.CreatedDate)] = new DateTime(2026, 11, 2)
        });
        var requestIds = new List<Guid>();
        var trialIds = new List<Guid>();
        for (var i = 0; i < 36; i++)
        {
            var id = Guid.NewGuid(); requestIds.Add(id);
            var trialId = Guid.NewGuid(); trialIds.Add(trialId);
            await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
            {
                [nameof(SampleRequest.SampleRequestId)] = id, [nameof(SampleRequest.CompanyId)] = company,
                [nameof(SampleRequest.CustomerId)] = customer, [nameof(SampleRequest.ManagerBy)] = owner,
                [nameof(SampleRequest.ProductId)] = product, [nameof(SampleRequest.FormulaId)] = formulaIds[i],
                [nameof(SampleRequest.IsActive)] = true, [nameof(SampleRequest.Status)] = "InProgress",
                [nameof(SampleRequest.InfoType)] = i % 2 == 0 ? "Báo giá" : "Thẻ màu (bộ)",
                [nameof(SampleRequest.CreatedDate)] = new DateTime(2026, 11, 2),
                [nameof(SampleRequest.ExternalId)] = $"TP_{i:D3}", [nameof(SampleRequest.ExpectedDeliveryDate)] = new DateTime(2026, 11, 7)
            });
            await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestWorkflowEvent))!, new()
            {
                [nameof(SampleRequestWorkflowEvent.Id)] = Guid.NewGuid(), [nameof(SampleRequestWorkflowEvent.CompanyId)] = company,
                [nameof(SampleRequestWorkflowEvent.SampleRequestId)] = id, [nameof(SampleRequestWorkflowEvent.FormulaId)] = formulaIds[i],
                [nameof(SampleRequestWorkflowEvent.EventType)] = SampleRequestWorkflowEvents.FormulaConfirmed,
                [nameof(SampleRequestWorkflowEvent.RecordedAt)] = new DateTime(2026, 11, 3)
            });
            await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestSampleTrial))!, new()
            {
                [nameof(SampleRequestSampleTrial.SampleRequestSampleTrialId)] = trialId,
                [nameof(SampleRequestSampleTrial.SampleRequestId)] = id, [nameof(SampleRequestSampleTrial.FormulaId)] = formulaIds[i],
                [nameof(SampleRequestSampleTrial.TrialNo)] = 1, [nameof(SampleRequestSampleTrial.IsActive)] = true,
                [nameof(SampleRequestSampleTrial.Status)] = i % 3 == 0 ? SampleTrialStatus.Approved : SampleTrialStatus.SampleSent,
                [nameof(SampleRequestSampleTrial.DeliveredSampleQuantityKg)] = i % 2 == 0 ? 0m : 2m,
                [nameof(SampleRequestSampleTrial.CustomerReplyNote)] = $"Feedback {i}",
                [nameof(SampleRequestSampleTrial.CreatedDate)] = new DateTime(2026, 11, 4)
            });
        }
        var excludedTrial = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestSampleTrial))!, new()
        {
            [nameof(SampleRequestSampleTrial.SampleRequestSampleTrialId)] = excludedTrial,
            [nameof(SampleRequestSampleTrial.SampleRequestId)] = requestIds[0], [nameof(SampleRequestSampleTrial.FormulaId)] = formulaIds[0],
            [nameof(SampleRequestSampleTrial.TrialNo)] = 2, [nameof(SampleRequestSampleTrial.IsActive)] = true,
            [nameof(SampleRequestSampleTrial.Status)] = SampleTrialStatus.SampleSent
        });
        var raw = from request in db.SampleRequests.AsNoTracking()
                  where request.CompanyId == company && request.IsActive && request.ManagerBy == owner
                  join trial in db.SampleRequestSampleTrials.Where(x => x.IsActive) on request.SampleRequestId equals trial.SampleRequestId into trials
                  from trial in trials.DefaultIfEmpty()
                  select new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = trial, TrialCount = 1 };
        var lab = SampleRequestLabWorkQuery.Enrich(raw, db);
        var decisions = SampleWorkDecisionQuery.Enrich(lab, db, db, "VND");
        var day = new DateTime(2026, 11, 6);
        var writable = db.SampleRequests.Where(x => x.CompanyId == company && x.ManagerBy == owner);
        var access = new SampleRequestDailyPriorityFactsPage.Access(SampleRequestWorkAudience.Sale, false, true, false, false, [owner]);
        // Time the current grouped path before SQL generation or query warmup.
        var watch = Stopwatch.StartNew();
        var once = await SampleRequestDailyPriorityFactsPage.ReadAsync(decisions.Where(x => x.Trial!.TrialNo == 1), raw,
            db, db, "VND", access, writable, day, 1, 15, CancellationToken.None);
        var onceStagesMs = watch.Elapsed.TotalMilliseconds;
        var page = once.Payload;
        Assert.Equal(new[] { customer }, once.CustomerIds);
        var current = await page.Rows.Select(x => new SampleRequestSampleTrialReportDto
        {
            SampleRequestId = x.SampleRequest.SampleRequestId, SampleRequestSampleTrialId = x.Trial == null ? null : x.Trial.SampleRequestSampleTrialId,
            ProductId = x.SampleRequest.ProductId, CustomerId = x.SampleRequest.CustomerId,
            SampleRequestExternalId = x.SampleRequest.ExternalId,
            CustomerReplyNote = x.Trial == null ? null : x.Trial.CustomerReplyNote
        }).ToListAsync();
        await page.AttachAsync(current, db, company, CancellationToken.None);
        current = current.OrderBy(page.Position).ToList();
        watch.Stop(); var newColdMs = watch.Elapsed.TotalMilliseconds;
        Assert.Equal(36, current.Count);
        Assert.DoesNotContain(current, x => x.SampleRequestId == outOfScope);
        Assert.DoesNotContain(current, x => x.SampleRequestId == otherOwner || x.SampleRequestSampleTrialId == excludedTrial);
        // Assert the fixture's expected payload directly. The retired projection repeats the
        // decision expression and can exhaust PostgreSQL memory before returning any rows.
        var byRequest = current.ToDictionary(x => x.SampleRequestId);
        for (var i = 0; i < requestIds.Count; i++)
        {
            var item = byRequest[requestIds[i]];
            Assert.Equal(trialIds[i], item.SampleRequestSampleTrialId);
            Assert.Equal(formulaIds[i], item.FormulaId);
            Assert.Equal($"Feedback {i}", item.CustomerReplyNote);
            var formulaIsVisible = i is not (1 or 2 or 3);
            var status = i % 3 == 0 ? "Completed" : "Approved";
            Assert.Equal(formulaIsVisible ? status : null, item.FormulaStatus);
            Assert.Equal(formulaIsVisible ? $"VU_{i}" : null, item.FormulaExternalId);
            Assert.Equal(formulaIsVisible ? owner : (Guid?)null, item.FormulaCreatedByEmployeeId);
            Assert.Equal(formulaIsVisible ? "Fixture author" : null, item.FormulaCreatedByName);
        }
        Assert.Equal(page.States.Select(x => (x.RequestId, x.TrialId)),
            current.Select(x => (x.SampleRequestId, x.SampleRequestSampleTrialId)));
        // The actual payload source no longer contains pricing/workflow decision joins or their CASE tree.
        var leanSourceSql = page.Rows.Select(x => x.SampleRequest.SampleRequestId).ToQueryString();
        Assert.DoesNotContain(db.Model.FindEntityType(typeof(ProductPricingVersion))!.GetTableName()!, leanSourceSql);
        Assert.DoesNotContain(db.Model.FindEntityType(typeof(SampleRequestWorkflowEvent))!.GetTableName()!, leanSourceSql);
        var onceSource = SampleRequestDailyPriorityFactsPage.ReadRows(decisions, db, db, "VND", access, writable).ToQueryString();
        Assert.DoesNotContain("'Lab'", onceSource);
        Assert.DoesNotContain("'President'", onceSource);
        Assert.DoesNotContain(once.Payload.States, x => x.RequestId == outOfScope || x.RequestId == otherOwner || x.TrialId == excludedTrial);
        output.WriteLine($"Grouped page-selection + page-facts: once={onceStagesMs:F2}ms");
        output.WriteLine($"30,000 Formula / 4,400 prices / 36 events / 36 trials; current payload first enumeration={newColdMs:F2}ms");
        var artifact = Path.Combine(postgres.RepositoryRoot, "artifacts", "daily-payload-performance");
        Directory.CreateDirectory(artifact);
        await File.WriteAllTextAsync(Path.Combine(artifact, "measurements.txt"), $"Cold enumeration including EF compile: {newColdMs:F2}ms. Formula=30000 prices=4400 events=36 trials=36 selectedRequests=36. Isolated partial payload fixture; not full HTTP endpoint.");
        await File.WriteAllTextAsync(Path.Combine(artifact, "facts-once.txt"),
            $"Grouped page-selection + page-facts (first enumeration of these query shapes): once={onceStagesMs:F2}ms. Verified customer scope, selected rows, effective formula and creator metadata. Formula=30000 prices=4400 events=36 trials=36. Isolated stage benchmark; not full HTTP endpoint.");
    }
}
