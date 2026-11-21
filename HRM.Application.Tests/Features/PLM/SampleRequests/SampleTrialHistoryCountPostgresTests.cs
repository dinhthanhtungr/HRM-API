using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleTrialHistoryCountPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task DirectHistoryCountPreservesVisibleRowsEmptyRequestAndPageTrialTotals()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var type in new[] { typeof(SampleRequest), typeof(SampleRequestSampleTrial) })
            await CreateTable(connection, db.Model.FindEntityType(type)!);
        var company = Guid.NewGuid(); var request = Guid.NewGuid(); var empty = Guid.NewGuid(); var hidden = Guid.NewGuid();
        foreach (var id in new[] { request, empty, hidden })
            await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new()
            { [nameof(SampleRequest.SampleRequestId)] = id, [nameof(SampleRequest.CompanyId)] = id == hidden ? Guid.NewGuid() : company,
                [nameof(SampleRequest.IsActive)] = true });
        foreach (var id in new[] { request, hidden })
            for (var no = 1; no <= 4; no++)
                await Insert(connection, db.Model.FindEntityType(typeof(SampleRequestSampleTrial))!, new()
                { [nameof(SampleRequestSampleTrial.SampleRequestSampleTrialId)] = Guid.NewGuid(),
                    [nameof(SampleRequestSampleTrial.SampleRequestId)] = id, [nameof(SampleRequestSampleTrial.TrialNo)] = no,
                    [nameof(SampleRequestSampleTrial.IsActive)] = no != 4 });
        var rows = SampleRequestSampleTrialReportQueryRules.HistoryRows(
            db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive), db.SampleRequestSampleTrials.Where(x => x.IsActive));
        Assert.Equal(4, await rows.CountAsync());
        Assert.Equal(1, await rows.Where(x => x.SampleRequest.SampleRequestId == empty).CountAsync());
        Assert.Equal(0, await rows.Where(x => x.SampleRequest.SampleRequestId == hidden).CountAsync());
        var page = await rows.Where(x => x.SampleRequest.SampleRequestId == request).OrderByDescending(x => x.Trial!.TrialNo)
            .Skip(1).Take(1).Select(x => new { x.Trial!.TrialNo, x.TrialCount }).SingleAsync();
        Assert.Equal(2, page.TrialNo);
        Assert.Equal(3, page.TrialCount);
        var sql = rows.Select(x => x.Trial == null ? (Guid?)null : x.Trial.SampleRequestSampleTrialId).ToQueryString();
        Assert.DoesNotContain("GROUP BY", sql);
    }
}
