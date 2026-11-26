using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

/// <summary>Executes the canonical decision and queue predicates on an isolated PostgreSQL cluster.</summary>
public sealed class SampleWorkDecisionPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    private sealed record Scenario(string Name, string Code, string Role, string Formula = "Draft",
        double Kg = 0, SampleTrialStatus? Trial = null, decimal? Delivered = null,
        bool Accepted = false, bool Finalized = false, bool Price = false, bool Quoted = false,
        bool Legacy = false, string Status = "InProgress", bool FormulaActive = true, bool SharedKg = false, bool Quotation = false,
        bool DeliveryPlan = false, bool CombinedPlan = false);

    [PostgresGroupStorageFact]
    public async Task DecisionsAndRoleBucketsTranslateBeforeCountingAndPaging()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var type in new[] { typeof(Customer), typeof(Product), typeof(SampleRequest), typeof(SampleRequestSampleTrial), typeof(Formula), typeof(ProductPricingVersion), typeof(SampleRequestWorkflowEvent) })
            await CreateTable(connection, db.Model.FindEntityType(type)!);
        Scenario[] cases =
        [
            new("01-chip-prepare", "ApproveFormula", "Lab"),
            new("02-chip-send", "SendSample", "Lab", Formula: "Approved"),
            new("03-chip-feedback", "FollowUpCustomer", "Sale", Trial: SampleTrialStatus.SampleSent, Delivered: 0),
            new("04-chip-rework", "ReworkFormula", "Lab", Trial: SampleTrialStatus.Failed, Delivered: 0),
            new("05-chip-choice", "ChooseAfterChip", "Sale", Trial: SampleTrialStatus.Approved, Delivered: 0),
            new("06-sale-decision", "CompleteRequest", "Sale", Formula: "Completed", Trial: SampleTrialStatus.Approved, Delivered: 0, Finalized: true),
            new("07-kg-prepare", "ApproveFormula", "Lab", Kg: 2),
            new("08-kg-price", "SendSample", "Lab", Formula: "Approved", Kg: 2),
            new("09-kg-quote", "SendSample", "Lab", Formula: "Approved", Kg: 2, Price: true),
            new("10-kg-price-feedback", "SendSample", "Lab", Formula: "Approved", Kg: 2, Price: true, Quoted: true),
            new("11-kg-send", "SendSample", "Lab", Formula: "Approved", Kg: 2, Accepted: true),
            new("12-kg-feedback", "FollowUpCustomer", "Sale", Trial: SampleTrialStatus.WaitingCustomerFeedback, Delivered: 2),
            new("13-kg-failed", "ReworkFormula", "Lab", Trial: SampleTrialStatus.Failed, Delivered: 2),
            new("14-legacy-send", "SendSample", "Lab", Formula: "Approved", Legacy: true),
            new("15-legacy-sent", "CompleteRequest", "Sale", Formula: "SampleSent", Legacy: true, Status: "SampleSent"),
            new("18-legacy-invalid", "ReviewSample", "LabAdmin", Legacy: true, Status: "SampleSent"),
            new("19-inactive-source", "ApproveFormula", "Lab", Formula: "Approved", Kg: 2, Accepted: true, Price: true, FormulaActive: false),
            new("16-unknown-delivery", "ReviewSample", "LabAdmin", Trial: SampleTrialStatus.SampleSent),
            new("20-shared-vu-kg", "ReviewSample", "LabAdmin", Formula: "Approved", SharedKg: true),
            new("21-quote-lab-finalize", "FinalizeQuotation", "Lab", Formula: "Approved", Accepted: true, Quoted: true, Quotation: true),
            new("22-quote-sale-complete", "CompleteRequest", "Sale", Formula: "Completed", Accepted: true, Quoted: true, Quotation: true),
            new("23-kg-accepted-price", "ApproveStandardPrice", "President", Formula: "SampleSent", Trial: SampleTrialStatus.Approved, Delivered: 2, Quotation: true),
            new("24-kg-accepted-quote", "SendQuotation", "Sale", Formula: "SampleSent", Trial: SampleTrialStatus.Approved, Delivered: 2, Price: true, Quotation: true),
            new("25-kg-accepted-feedback", "PriceFeedback", "Sale", Formula: "SampleSent", Trial: SampleTrialStatus.Approved, Delivered: 2, Price: true, Quoted: true),
            new("26-kg-both-accepted", "CompleteRequest", "Sale", Formula: "SampleSent", Trial: SampleTrialStatus.Approved, Delivered: 2, Accepted: true),
            new("17-closed", "", "", Status: "Completed"),
            new("27-kg-repeat-plan", "ApproveFormula", "Lab", Formula: "Approved", Kg: 5, Trial: SampleTrialStatus.Approved, Delivered: 2, DeliveryPlan: true),
            new("28-kg-repeat-select-vu", "SendSample", "Lab", Formula: "SampleSent", Kg: 5, Trial: SampleTrialStatus.Failed, Delivered: 2, DeliveryPlan: true),
            new("29-chip-sent-kg-next", "SendSample", "Lab", Formula: "SampleSent", Kg: 100, Trial: SampleTrialStatus.WaitingCustomerFeedback, Delivered: 0, CombinedPlan: true),
            new("30-chip-sample-no-quotation", "CompleteRequest", "Sale", Formula: "SampleSent", Kg: 100, Trial: SampleTrialStatus.Approved, Delivered: 100)
        ];
        var company = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Customer))!, new()
        {
            [nameof(Customer.CustomerId)] = Guid.Empty, [nameof(Customer.CompanyId)] = company,
            [nameof(Customer.ExternalId)] = "KH_TEST", [nameof(Customer.IsActive)] = true
        });
        var accepted = new List<Guid>();
        var finalized = new List<Guid>();
        foreach (var item in cases)
        {
            var request = Guid.NewGuid(); var formula = Guid.NewGuid(); var product = Guid.NewGuid();
            if (item.Accepted) accepted.Add(request);
            if (item.Finalized) finalized.Add(request);
            await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
            {
                [nameof(SampleRequest.SampleRequestId)] = request, [nameof(SampleRequest.ExternalId)] = item.Name,
                [nameof(SampleRequest.CompanyId)] = company, [nameof(SampleRequest.ProductId)] = product,
                [nameof(SampleRequest.CreatedDate)] = new DateTime(2026, item.Legacy ? 10 : 11, 1),
                [nameof(SampleRequest.Status)] = item.Status, [nameof(SampleRequest.SampleQuantity)] = item.Kg,
                [nameof(SampleRequest.FormulaId)] = formula, [nameof(SampleRequest.InfoType)] = item.Quotation ? "Quotation" : "ColorCard",
                [nameof(SampleRequest.LabReceivedDate)] = item.Quotation ? new DateTime(2026, 11, 2) : null,
                [nameof(SampleRequest.IsActive)] = true,
                [nameof(SampleRequest.RealPriceQuoteDate)] = item.Quoted ? new DateTime(2026, 11, 2) : null
            });
            await Insert(connection, db.Model.FindEntityType(typeof(Formula))!, new()
            {
                [nameof(Formula.FormulaId)] = formula, [nameof(Formula.ProductId)] = product,
                [nameof(Formula.CompanyId)] = company, [nameof(Formula.IsActive)] = item.FormulaActive, [nameof(Formula.Status)] = item.Formula
            });
            if (item.Trial is { } status)
                await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestSampleTrial))!, new()
                {
                    [nameof(SampleRequestSampleTrial.SampleRequestSampleTrialId)] = Guid.NewGuid(),
                    [nameof(SampleRequestSampleTrial.SampleRequestId)] = request,
                    [nameof(SampleRequestSampleTrial.FormulaId)] = formula,
                    [nameof(SampleRequestSampleTrial.Status)] = (int)status,
                    [nameof(SampleRequestSampleTrial.DeliveredSampleQuantityKg)] = item.Delivered,
                    [nameof(SampleRequestSampleTrial.SentDate)] = new DateTime(2026, 11, 2),
                    [nameof(SampleRequestSampleTrial.CreatedDate)] = new DateTime(2026, 11, 2),
                    [nameof(SampleRequestSampleTrial.IsActive)] = true
                });
            if (item.DeliveryPlan || item.CombinedPlan)
                await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestWorkflowEvent))!, new()
                {
                    [nameof(SampleRequestWorkflowEvent.Id)] = Guid.NewGuid(),
                    [nameof(SampleRequestWorkflowEvent.CompanyId)] = company,
                    [nameof(SampleRequestWorkflowEvent.SampleRequestId)] = request,
                    [nameof(SampleRequestWorkflowEvent.FormulaId)] = formula,
                    [nameof(SampleRequestWorkflowEvent.EventType)] = item.CombinedPlan
                        ? HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFlowRules.ChipThenSampleRequested
                        : HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFlowRules.KgRequested,
                    [nameof(SampleRequestWorkflowEvent.RecordedAt)] = new DateTime(2026, 11, item.CombinedPlan ? 1 : 3)
                });
            if (item.SharedKg)
            {
                var historyRequest = Guid.NewGuid();
                await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
                {
                    [nameof(SampleRequest.SampleRequestId)] = historyRequest,
                    [nameof(SampleRequest.ExternalId)] = "kg-history-source",
                    [nameof(SampleRequest.CompanyId)] = company,
                    [nameof(SampleRequest.ProductId)] = product,
                    [nameof(SampleRequest.IsActive)] = true
                });
                await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestSampleTrial))!, new()
                {
                    [nameof(SampleRequestSampleTrial.SampleRequestSampleTrialId)] = Guid.NewGuid(),
                    [nameof(SampleRequestSampleTrial.SampleRequestId)] = historyRequest,
                    [nameof(SampleRequestSampleTrial.FormulaId)] = formula,
                    [nameof(SampleRequestSampleTrial.DeliveredSampleQuantityKg)] = 2m,
                    [nameof(SampleRequestSampleTrial.SentDate)] = new DateTime(2026, 11, 2),
                    [nameof(SampleRequestSampleTrial.IsActive)] = true
                });
            }
            if (item.Price)
                await Insert(connection, db.Model.FindEntityType(typeof(ProductPricingVersion))!, new()
                {
                    [nameof(ProductPricingVersion.ProductPricingVersionId)] = Guid.NewGuid(),
                    [nameof(ProductPricingVersion.CompanyId)] = company, [nameof(ProductPricingVersion.ProductId)] = product,
                    [nameof(ProductPricingVersion.SourceFormulaId)] = formula, [nameof(ProductPricingVersion.Currency)] = "VND",
                    [nameof(ProductPricingVersion.Status)] = (int)ProductPricingStatus.Approved,
                    [nameof(ProductPricingVersion.StandardSellingPrice)] = 100m, [nameof(ProductPricingVersion.Version)] = 1,
                    [nameof(ProductPricingVersion.IsActive)] = true
                });
        }
        var acceptedIds = accepted.ToArray(); var finalizedIds = finalized.ToArray();
        // Event enrichment is supplied as explicit scalar inputs; this test targets decision/queue SQL composition.
        var source = from request in db.SampleRequests.Where(x => x.CompanyId == company && x.ExternalId != "kg-history-source")
                     join trial in db.SampleRequestSampleTrials on request.SampleRequestId equals trial.SampleRequestId into trials
                     from trial in trials.DefaultIfEmpty()
                     select new SampleRequestSampleTrialReportRow
                     {
                         SampleRequest = request, Trial = trial, ConfirmedFormulaId = request.FormulaId,
                         LabFormulaConfirmedAt = request.LabReceivedDate,
                         HasAcceptedCustomerPrice = acceptedIds.Contains(request.SampleRequestId),
                         HasFinalizedChip = finalizedIds.Contains(request.SampleRequestId)
                     };
        var decisions = SampleWorkDecisionQuery.Enrich(source, db, db, "VND");
        var actual = await decisions.Select(x => new { x.SampleRequest.ExternalId, x.WorkDecision!.Code, x.WorkDecision.Role, x.WorkDecision.Stage }).ToListAsync();
        // History reads these scalar facts once and computes the same decision after paging.
        var deferredFacts = await SampleWorkDecisionQuery.ReadFacts(source.OrderBy(x => x.SampleRequest.ExternalId).Take(100), db, db, "VND")
            .Select(x => new { x.Row.SampleRequest.ExternalId, x.Facts }).ToListAsync();
        foreach (var row in deferredFacts)
        {
            var deferred = SampleWorkDecisionRules.Resolve(row.Facts);
            var original = Assert.Single(actual, x => x.ExternalId == row.ExternalId);
            Assert.Equal(original.Code, deferred.Code);
            Assert.Equal(original.Role, deferred.Role);
            Assert.Equal(original.Stage, deferred.Stage);
        }
        foreach (var item in cases)
        {
            var found = Assert.Single(actual, x => x.ExternalId == item.Name);
            Assert.True(item.Code == found.Code, $"{item.Name}: expected {item.Code}, got {found.Code}"); Assert.Equal(item.Role, found.Role);
            if (item.Name == "16-unknown-delivery") Assert.Equal("NeedsReview", found.Stage);
        }
        foreach (var audience in new[] { SampleRequestWorkAudience.Lab, SampleRequestWorkAudience.Sale, SampleRequestWorkAudience.LabAdmin })
        {
            var queue = SampleRequestWorkQueueQuery.Apply(decisions, new GetSampleRequestSampleTrialsQuery
            { WorkAudience = audience, WorkBucket = SampleRequestWorkBucket.ActionRequired }, db);
            Assert.Contains("WHERE", queue.ToQueryString());
            var expected = cases.Where(x => audience == SampleRequestWorkAudience.LabAdmin
                ? x.Role is "Lab" or "LabAdmin" : x.Role == audience.ToString() ||
                    audience == SampleRequestWorkAudience.Sale && x.Name == "29-chip-sent-kg-next").Select(x => x.Name).Order().ToArray();
            Assert.Equal(expected.Length, await queue.CountAsync());
            var menuRows = SampleRequestDailyPriorityFactsPage.ReadRows(source, db, db, "VND",
                new(audience, true, true, true, true, [Guid.Empty]), db.SampleRequests.Where(x => x.CompanyId == company), forMenu: true)
                .Select(row => new SampleRequestDailyPriorityFactsPage.MenuRow
                {
                    Row = row,
                    InAll = db.SampleRequests.Any(x => x.CompanyId == company && x.SampleRequestId == row.RequestId),
                    InOperational = db.SampleRequests.Any(x => x.CompanyId == company && x.SampleRequestId == row.RequestId)
                });
            var menuCounts = await SampleRequestDailyPriorityFactsPage.CountMenuRowsAsync(menuRows.AsAsyncEnumerable(), audience,
                Guid.Empty, false, new(2026, 11, 7), CancellationToken.None);
            Assert.Equal(expected.Length, menuCounts["workBuckets"]["actionRequired"]);
            Assert.Equal(cases.Length, menuCounts["workBuckets"]["all"]);
            var page = queue.OrderBy(x => x.SampleRequest.ExternalId).Skip(1).Take(3).Select(x => x.SampleRequest.ExternalId);
            Assert.Contains("LIMIT", page.ToQueryString());
            Assert.Equal(expected.Skip(1).Take(3), await page.ToArrayAsync());
            if (audience != SampleRequestWorkAudience.LabAdmin)
            {
                // Execute the new facts-only role filter against the same isolated rows,
                // including failed trials, legacy records and price states above.
                var factsRows = SampleWorkDecisionQuery.ReadFacts(source, db, db, "VND")
                    .OrderBy(x => x.Row.SampleRequest.SampleRequestId)
                    .Select(x => new SampleRequestDailyPriorityFactsPage.ReadRow
                    {
                        CustomerId = Guid.Empty, Status = x.Row.SampleRequest.Status,
                        SaleWritable = true,
                        State = new SampleRequestDailyPayloadPage.State(x.Row.SampleRequest.SampleRequestId,
                            x.Row.Trial == null ? null : x.Row.Trial.SampleRequestSampleTrialId,
                            x.Facts, null, null, false)
                    });
                var factsPage = await SampleRequestDailyPriorityFactsPage.FromRowsAsync(factsRows.AsAsyncEnumerable(),
                    source, new(audience, true, true, false, false, [], audience.ToString()),
                    new(2026, 11, 7), 1, 15, CancellationToken.None);
                var expectedIds = await queue.Select(x => x.SampleRequest.SampleRequestId).ToArrayAsync();
                Assert.Equal(expectedIds.Order(), factsPage.Payload.States.Select(x => x.RequestId).Order());
                Assert.Equal(expectedIds.Length > 0 ? 1 : 0, factsPage.TotalCustomers);
            }
        }
        var labProgress = SampleRequestWorkQueueQuery.Apply(decisions, new GetSampleRequestSampleTrialsQuery
        { WorkAudience = SampleRequestWorkAudience.LabAdmin, WorkBucket = SampleRequestWorkBucket.LabProgress }, db);
        var expectedLabProgress = cases.Where(x => x.Role == "Lab").Select(x => x.Name).Order().ToArray();
        Assert.Equal(expectedLabProgress.Length, await labProgress.CountAsync());
        Assert.Equal(expectedLabProgress.Skip(1).Take(3), await labProgress.OrderBy(x => x.SampleRequest.ExternalId)
            .Skip(1).Take(3).Select(x => x.SampleRequest.ExternalId).ToArrayAsync());
        foreach (var (bucket, code) in new[] { (SampleRequestWorkBucket.Rework, "ReworkFormula"),
                     (SampleRequestWorkBucket.NewRequestRequired, "CreateNewRequest"), (SampleRequestWorkBucket.WaitingPrice, "ApproveStandardPrice") })
        {
            var queue = SampleRequestWorkQueueQuery.Apply(decisions, new GetSampleRequestSampleTrialsQuery { WorkBucket = bucket }, db);
            Assert.Equal(cases.Count(x => x.Code == code), await queue.CountAsync());
            Assert.All(await queue.Select(x => x.WorkDecision!.Code).ToArrayAsync(), x => Assert.Equal(code, x));
        }
        await AssertDueDateParity(connection, db, company);
    }

    private static async Task AssertDueDateParity(NpgsqlConnection connection, ApplicationDbContext db, Guid company)
    {
        var today = new DateOnly(2026, 11, 4);
        var past = new DateTime(2026, 11, 3);
        var future = new DateTime(2026, 11, 6);
        var cases = new[]
        {
            new { Name = "due-planned-future", Expected = (DateTime?)future, Requested = (DateTime?)past, Received = (DateTime?)null, Days = (int?)null, Quote = (DateTime?)null, Info = "ColorCard", Kg = 0d, Overdue = false },
            new { Name = "due-planned-past", Expected = (DateTime?)past, Requested = (DateTime?)future, Received = (DateTime?)null, Days = (int?)null, Quote = (DateTime?)null, Info = "ColorCard", Kg = 0d, Overdue = true },
            new { Name = "due-requested", Expected = (DateTime?)null, Requested = (DateTime?)past, Received = (DateTime?)null, Days = (int?)null, Quote = (DateTime?)null, Info = "ColorCard", Kg = 0d, Overdue = true },
            new { Name = "due-receipt-past", Expected = (DateTime?)null, Requested = (DateTime?)null, Received = (DateTime?)new DateTime(2026, 11, 1, 14, 30, 0), Days = (int?)2, Quote = (DateTime?)null, Info = "ColorCard", Kg = 0d, Overdue = true },
            new { Name = "due-receipt-future", Expected = (DateTime?)null, Requested = (DateTime?)null, Received = (DateTime?)past, Days = (int?)2, Quote = (DateTime?)null, Info = "ColorCard", Kg = 0d, Overdue = false },
            new { Name = "due-overflow-rejected", Expected = (DateTime?)null, Requested = (DateTime?)null, Received = (DateTime?)past, Days = (int?)int.MaxValue, Quote = (DateTime?)null, Info = "ColorCard", Kg = 0d, Overdue = false },
            new { Name = "due-negative-rejected", Expected = (DateTime?)null, Requested = (DateTime?)null, Received = (DateTime?)past, Days = (int?)-1, Quote = (DateTime?)null, Info = "ColorCard", Kg = 0d, Overdue = false },
            new { Name = "due-quote-past", Expected = (DateTime?)future, Requested = (DateTime?)future, Received = (DateTime?)null, Days = (int?)null, Quote = (DateTime?)past, Info = "Quotation", Kg = 0d, Overdue = true },
            new { Name = "due-quote-missing", Expected = (DateTime?)past, Requested = (DateTime?)past, Received = (DateTime?)null, Days = (int?)null, Quote = (DateTime?)null, Info = "Quotation", Kg = 0d, Overdue = false },
            new { Name = "due-quote-with-kg", Expected = (DateTime?)past, Requested = (DateTime?)future, Received = (DateTime?)null, Days = (int?)null, Quote = (DateTime?)future, Info = "Quotation", Kg = 2d, Overdue = true }
        };
        foreach (var item in cases)
            await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
            {
                [nameof(SampleRequest.SampleRequestId)] = Guid.NewGuid(), [nameof(SampleRequest.CompanyId)] = company,
                [nameof(SampleRequest.ExternalId)] = item.Name, [nameof(SampleRequest.IsActive)] = true,
                [nameof(SampleRequest.Status)] = "InProgress", [nameof(SampleRequest.InfoType)] = item.Info,
                [nameof(SampleRequest.ExpectedDeliveryDate)] = item.Expected, [nameof(SampleRequest.RequestDeliveryDate)] = item.Requested,
                [nameof(SampleRequest.LabReceivedDate)] = item.Received, [nameof(SampleRequest.NumberDeliverySampleDate)] = item.Days,
                [nameof(SampleRequest.ExpectedPriceQuoteDate)] = item.Quote, [nameof(SampleRequest.SampleQuantity)] = item.Kg
            });
        var rows = db.SampleRequests.Where(x => x.CompanyId == company && x.ExternalId.StartsWith("due-"))
            .Select(x => new SampleRequestSampleTrialReportRow
            {
                SampleRequest = x,
                WorkDecision = new SampleWorkDecision { Code = "ApproveFormula", Role = "Lab", Stage = "PrepareChip" }
            });
        var overdue = await SampleRequestWorkQueueQuery.Apply(rows, new GetSampleRequestSampleTrialsQuery
            { WorkBucket = SampleRequestWorkBucket.Overdue, DailyWorkDate = today }, db)
            .Select(x => x.SampleRequest.ExternalId).OrderBy(x => x).ToArrayAsync();
        Assert.Equal(cases.Where(x => x.Overdue).Select(x => x.Name).OrderBy(x => x), overdue);
        foreach (var item in cases)
        {
            var displayed = new HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials.SampleRequestSampleTrialReportDto
            {
                InfoType = item.Info, RequestedSampleQuantity = item.Kg, SampleRequestStatus = "InProgress",
                ExpectedDeliveryDate = item.Expected, RequestDeliveryDate = item.Requested, ExpectedPriceQuoteDate = item.Quote,
                LabReceivedDate = item.Received, NumberDeliverySampleDate = item.Days
            };
            var (date, _) = SampleRequestDailyWorkRules.ResolveDueDate(displayed);
            Assert.Equal(item.Overdue, date.HasValue && DateOnly.FromDateTime(date.Value) < today);
        }
    }

    internal static async Task CreateTable(NpgsqlConnection connection, IEntityType entity)
    {
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        // Identifiers and SQL types come exclusively from the trusted EF model, never request input.
        await using var schema = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{table.Schema}\"", connection);
        await schema.ExecuteNonQueryAsync();
        var columns = entity.GetProperties().Select(p => $"\"{p.GetColumnName(table)}\" {p.GetRelationalTypeMapping().StoreType} NULL");
        var keys = entity.FindPrimaryKey()!.Properties.Select(p => $"\"{p.GetColumnName(table)}\"");
        await using var command = new NpgsqlCommand($"CREATE TABLE \"{table.Schema}\".\"{table.Name}\" ({string.Join(',', columns)}, PRIMARY KEY ({string.Join(',', keys)}))", connection);
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task Insert(NpgsqlConnection connection, IEntityType entity, Dictionary<string, object?> values)
    {
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        foreach (var property in entity.GetProperties().Where(x => !x.IsNullable && !values.ContainsKey(x.Name)))
            values[property.Name] = property.ClrType == typeof(string) ? "" : Activator.CreateInstance(property.ClrType);
        var populated = values.Where(x => x.Value is not null).ToArray();
        var columns = populated.Select(x => $"\"{entity.FindProperty(x.Key)!.GetColumnName(table)}\"");
        await using var command = new NpgsqlCommand($"INSERT INTO \"{table.Schema}\".\"{table.Name}\" ({string.Join(',', columns)}) VALUES ({string.Join(',', populated.Select((_, i) => "@p" + i))})", connection);
        for (var i = 0; i < populated.Length; i++)
        {
            var value = populated[i].Value!;
            var converter = entity.FindProperty(populated[i].Key)!.GetTypeMapping().Converter;
            if (converter is not null) value = converter.ConvertToProvider(value)!;
            command.Parameters.AddWithValue("p" + i, value);
        }
        await command.ExecuteNonQueryAsync();
    }
}
