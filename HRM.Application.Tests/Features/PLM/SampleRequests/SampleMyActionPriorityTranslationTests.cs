using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleMyActionPriorityTranslationTests
{
    [Theory]
    [InlineData(SampleRequestWorkAudience.Sale, SampleRequestWorkBucket.MyActionRequired, "Sale")]
    [InlineData(SampleRequestWorkAudience.Lab, SampleRequestWorkBucket.ActionRequired, "Lab")]
    public void FilteredAudienceFactsTranslateWithoutOpeningDatabase(
        SampleRequestWorkAudience audience, SampleRequestWorkBucket bucket, string role)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only").Options);
        var company = Guid.NewGuid(); var owner = Guid.NewGuid();
        var writable = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive && x.ManagerBy == owner);
        var visible = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive);
        if (audience == SampleRequestWorkAudience.Sale)
            visible = SampleRequestWorkQueueQuery.ApplyMySaleOwnership(visible, writable, [owner], owner);
        var source = from request in visible
                     join candidate in db.SampleRequestSampleTrials.Where(x => x.IsActive)
                         on request.SampleRequestId equals candidate.SampleRequestId into trials
                     from trial in trials.DefaultIfEmpty()
                     select new SampleRequestSampleTrialReportRow { SampleRequest = request, Trial = trial, TrialCount = 0 };
        var lab = SampleRequestLabWorkQuery.Enrich(source, db);
        var canonical = SampleWorkDecisionQuery.Enrich(lab, db, db, "VND");
        var filtered = SampleRequestWorkQueueQuery.Apply(canonical, new()
        {
            WorkAudience = audience,
            WorkBucket = bucket
        }, db);
        var sql = SampleRequestDailyPriorityFactsPage.ReadRows(filtered, db, db, "VND",
            new(audience, audience == SampleRequestWorkAudience.Lab, audience == SampleRequestWorkAudience.Sale,
                false, false, [owner]), writable).ToQueryString();
        Assert.Contains("WHERE", sql);
        Assert.Contains("ORDER BY", sql);
        if (audience == SampleRequestWorkAudience.Sale) Assert.Contains("ManagerBy", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains(role, sql);

        var openFacts = SampleRequestDailyPriorityFactsPage.ReadRows(
            lab.Where(x => x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled"), db, db, "VND",
            new(audience, audience == SampleRequestWorkAudience.Lab, audience == SampleRequestWorkAudience.Sale,
                false, false, [owner], role), writable).ToQueryString();
        Assert.Contains("CompanyId", openFacts);
        Assert.Contains("ORDER BY", openFacts);
        Assert.True(openFacts.Length < sql.Length, "Facts-only SQL must not repeat the decision tree in WHERE.");
    }
}
