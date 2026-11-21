using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleAdminCompletionPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ChipAndKgDeliveriesCountWithoutAnyApprovedPriceOnPostgres()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var tables = new[] { "SampleRequest", "SampleRequestSampleTrial", "SampleRequestWorkflowEvent", "Formula", "ProductPricingVersion", "Branch" };
        foreach (var type in db.Model.GetEntityTypes().Where(x => tables.Contains(x.ClrType.Name))) await CreateTable(connection, type);
        var company = Guid.NewGuid(); var id = Guid.NewGuid(); var chipId = Guid.NewGuid(); var day = new DateTime(2026, 11, 11);
        foreach (var requestId in new[] { id, chipId })
        {
            await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
            {
                [nameof(SampleRequest.SampleRequestId)] = requestId, [nameof(SampleRequest.CompanyId)] = company,
                [nameof(SampleRequest.CreatedDate)] = day, [nameof(SampleRequest.IsActive)] = true,
                [nameof(SampleRequest.Status)] = "SampleSent", [nameof(SampleRequest.InfoType)] = requestId == id ? "Báo giá" : "Thẻ màu (1)",
                [nameof(SampleRequest.SampleQuantity)] = requestId == id ? 2d : 0d
            });
            await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestSampleTrial))!, new()
            {
                [nameof(SampleRequestSampleTrial.SampleRequestSampleTrialId)] = Guid.NewGuid(),
                [nameof(SampleRequestSampleTrial.SampleRequestId)] = requestId, [nameof(SampleRequestSampleTrial.TrialNo)] = 1,
                [nameof(SampleRequestSampleTrial.IsActive)] = true, [nameof(SampleRequestSampleTrial.SentDate)] = day,
                [nameof(SampleRequestSampleTrial.CreatedDate)] = day,
                [nameof(SampleRequestSampleTrial.Status)] = SampleTrialStatus.SampleSent,
                [nameof(SampleRequestSampleTrial.DeliveredSampleQuantityKg)] = requestId == id ? 2m : 0m
            });
        }
        var source = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive);
        var records = SampleRequestAdminReportQuery.Build(source, db, db.ProductPricingVersions);
        Assert.Equal(2, await source.CountAsync());
        Assert.Equal(day, await db.SampleRequestSampleTrials.Where(t => t.SampleRequestId == id && t.SampleRequest.CompanyId == company && t.IsActive && t.DeliveredSampleQuantityKg > 0).Select(t => t.SentDate).SingleAsync());
        var state = await records.Where(x => x.SampleRequestId == id).Select(x => new { x.LabCompletedDate, x.LabCompletedKind }).SingleAsync();
        Assert.Equal(day, state.LabCompletedDate);
        var groups = await SampleRequestAdminReportQuery.Filter(records, SampleRequestAdminMetric.Completed, day, day.AddDays(1))
            .GroupBy(x => x.LabCompletedKind).Select(g => new { Kind = g.Key, Count = g.Count() }).ToListAsync();
        Assert.Equal(2, groups.Count);
        Assert.Equal(1, groups.Single(x => x.Kind == "sample").Count);
        Assert.Equal(1, groups.Single(x => x.Kind == "chip").Count);
        Assert.Equal(2, groups.Sum(x => x.Count));
        // A failed chip goes back to Lab work; it must not remain a completed delivery.
        await db.SampleRequestSampleTrials.Where(x => x.SampleRequestId == chipId).ExecuteUpdateAsync(update => update
            .SetProperty(x => x.Status, SampleTrialStatus.Failed).SetProperty(x => x.CustomerReplyDate, day.AddHours(1)));
        Assert.Equal(1, await SampleRequestAdminReportQuery.Filter(records, SampleRequestAdminMetric.Completed, day, day.AddDays(1)).CountAsync());
    }
}
