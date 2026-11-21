using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleWorkChanges;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkChangesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task InvalidBatchFailsBeforeReadingData(int count)
    {
        var handler = new GetSampleWorkChangesQueryHandler(null!, null!, null!, null!, null!, null!);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new()
        { SampleRequestIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray() }, default));
    }

    [Fact]
    public async Task SaleCannotProbeLabQueueAndInvalidTeamIsRejected()
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var handler = new GetSampleWorkChangesQueryHandler(null!, null!, null!, user, null!, null!);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new()
        { SampleRequestIds = [Guid.NewGuid()], Audience = SampleRequestWorkAudience.Lab }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new()
        { SampleRequestIds = [Guid.NewGuid()], Audience = SampleRequestWorkAudience.Sale, LabTeam = SampleLabTeamFilter.All }, default));
    }

    [Fact]
    public void VersionProbeTranslatesToNarrowPostgresProjectionWithoutReportSortOrCount()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        Guid[] ids = [Guid.NewGuid(), Guid.NewGuid()];
        var rows = db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && ids.Contains(x.SampleRequestId));
        var sql = GetSampleWorkChangesQueryHandler.BuildRevisions(rows, db, company).ToQueryString();
        Assert.Contains("max(", sql);
        Assert.Contains("ANY", sql);
        Assert.DoesNotContain("ORDER BY", sql);
        Assert.DoesNotContain("count(", sql);
        Assert.DoesNotContain("FormulaMaterials", sql);
        Assert.DoesNotContain("StandardSellingPrice", sql);
    }
}
