using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestDailyWorkSqlTests
{
    [Fact]
    public void CreatorLeaders_TranslateScopedGroupJoinForPostgres()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql().Options);
        var sql = SampleRequestFormulaCreatorLeaders.Build(db.MemberInGroups, Guid.NewGuid(), [Guid.NewGuid()])
            .ToQueryString();
        Assert.Contains("JOIN", sql);
        Assert.Contains("DISTINCT", sql);
        Assert.Contains("@companyId", sql);
    }

    [Fact]
    public void DelayedLabBacklog_TranslatesQuoteRequestScopeBeforePagination()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql().Options);
        var companyId = Guid.NewGuid();
        var rows =
            from x in db.SampleRequests.Where(x => x.CompanyId == companyId && x.IsActive)
            join trial in db.SampleRequestSampleTrials.Where(x => x.IsActive)
                on x.SampleRequestId equals trial.SampleRequestId into trials
            from trial in trials.DefaultIfEmpty()
            select new SampleRequestSampleTrialReportRow
            {
                SampleRequest = x,
                Trial = trial,
                TrialCount = null
            };

        rows = SampleRequestLabWorkQuery.Enrich(rows, db);

        var sql = SampleRequestSampleTrialReportQueryRules.ApplyReportType(rows,
                new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 10, 30) })
            .OrderBy(x => x.SampleRequest.SampleRequestId).Take(15).ToQueryString();

        Assert.Contains("@>", sql);
        Assert.Contains("SampleRequestPriceQuoteRequested", sql);
        Assert.Contains("formulaId", sql);
        Assert.Contains("EXISTS", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("LabFormulaConfirmed", sql);
        Assert.Contains("Status changed to Approved", sql);
    }
}
