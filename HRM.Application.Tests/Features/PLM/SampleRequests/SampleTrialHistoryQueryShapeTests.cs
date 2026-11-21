using System.Diagnostics;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit.Abstractions;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

/// <summary>Diagnoses history SQL expansion without opening a database or logging business SQL.</summary>
public sealed class SampleTrialHistoryQueryShapeTests(ITestOutputHelper output)
{
    [Fact]
    public void HistoryOptimizationDoesNotChangeQueueOrGroupedReportPaths()
    {
        var id = Guid.NewGuid();
        Assert.True(new GetSampleRequestSampleTrialsQuery { SampleRequestId = id, IncludeTrialHistory = true }.UseTrialHistoryPayload);
        Assert.False(new GetSampleRequestSampleTrialsQuery { IncludeTrialHistory = true }.UseTrialHistoryPayload);
        Assert.False(new GetSampleRequestSampleTrialsQuery { SampleRequestId = id }.UseTrialHistoryPayload);
        Assert.False(new GetSampleRequestSampleTrialsQuery { SampleRequestId = id, IncludeTrialHistory = true,
            WorkAudience = SampleRequestWorkAudience.Lab }.UseTrialHistoryPayload);
        Assert.False(new GetSampleRequestSampleTrialsQuery { SampleRequestId = id, IncludeTrialHistory = true,
            GroupByCustomer = true }.UseTrialHistoryPayload);
    }
    [Fact]
    public void HistoryDecisionProjectionExpandsSqlWithoutReadingAnyDatabase()
    {
        using var connection = new NpgsqlConnection();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var company = Guid.NewGuid();
        var id = Guid.NewGuid();
        var rows = from request in db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive && x.SampleRequestId == id)
                   join trial in db.SampleRequestSampleTrials.Where(x => x.IsActive)
                       on request.SampleRequestId equals trial.SampleRequestId into trials
                   from trial in trials.DefaultIfEmpty()
                   select new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = trial };
        var facts = SampleRequestLabWorkQuery.Enrich(rows, db);
        var history = new GetSampleRequestSampleTrialsQuery { SampleRequestId = id, IncludeTrialHistory = true, PageSize = 100 };
        Assert.True(history.UseTrialHistoryPayload);
        var watch = Stopwatch.StartNew();
        var leanSql = facts.OrderByDescending(x => x.Trial!.TrialNo).Take(100)
            .Select(x => new { x.SampleRequest.SampleRequestId, x.Trial!.TrialNo, x.LabFormulaConfirmedAt }).ToQueryString();
        var leanMs = watch.ElapsedMilliseconds;
        watch.Restart();
        var factSql = SampleWorkDecisionQuery.ReadFacts(facts.OrderByDescending(x => x.Trial!.TrialNo).Take(100), db, db, "VND")
            .Select(x => new { x.Row.Trial!.SampleRequestSampleTrialId, x.Facts }).ToQueryString();
        var factMs = watch.ElapsedMilliseconds;
        watch.Restart();
        var decisionSql = SampleWorkDecisionQuery.Enrich(facts, db, db, "VND")
            .OrderByDescending(x => x.Trial!.TrialNo).Take(100)
            .Select(x => new { x.SampleRequest.SampleRequestId, x.Trial!.TrialNo, x.LabFormulaConfirmedAt,
                x.WorkDecision, NeedsSchedule = x.NeedsSchedule && (x.WorkDecision!.Role == "Lab" || x.WorkDecision.Role == "LabAdmin") })
            .ToQueryString();
        output.WriteLine($"facts: {leanMs} ms, {leanSql.Length} SQL characters");
        output.WriteLine($"bounded facts before in-memory decision: {factMs} ms, {factSql.Length} SQL characters");
        output.WriteLine($"with decision: {watch.ElapsedMilliseconds} ms, {decisionSql.Length} SQL characters");
        Assert.True(decisionSql.Length > leanSql.Length);
        Assert.True(factSql.Length < decisionSql.Length / 10, "Bounded history facts must not expand the decision expression.");
        Assert.DoesNotContain("ChooseAfterPrice", leanSql);
        Assert.True(leanSql.Length < 100_000, "History SQL must not include the expanded workflow decision.");
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void WorkflowFormulaMetadataIsBoundedAndDoesNotRepeatFormulaFields()
    {
        using var connection = new NpgsqlConnection();
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var id = Guid.NewGuid(); var company = Guid.NewGuid();
        var rows = SampleRequestSampleTrialReportQueryRules.HistoryRows(
            db.SampleRequests.Where(x => x.CompanyId == company && x.SampleRequestId == id && x.IsActive),
            db.SampleRequestSampleTrials.Where(x => x.IsActive && x.SampleRequestId == id));
        var facts = SampleRequestLabWorkQuery.Enrich(rows, db).OrderByDescending(x => x.Trial!.TrialNo).Take(1);
        var watch = Stopwatch.StartNew();
        var metadata = SampleRequestDailyPayloadPage.WorkflowFormulaMetadata(facts).ToQueryString();
        output.WriteLine($"workflow formula metadata: {watch.ElapsedMilliseconds} ms, {metadata.Length} SQL characters");
        Assert.Contains("LIMIT", metadata);
        Assert.DoesNotContain("ChooseAfterPrice", metadata);
        Assert.DoesNotContain("GROUP BY", metadata);
        Assert.True(metadata.Length < 50_000);
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }
}
