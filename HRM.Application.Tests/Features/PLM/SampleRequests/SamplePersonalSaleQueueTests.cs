using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SamplePersonalSaleQueueTests
{
    [Fact]
    public void ManagedPermissionDoesNotIncludeTeamInPersonalQueue()
    {
        var actor = Guid.NewGuid();
        var subordinate = Guid.NewGuid();
        var own = new SampleRequest { SampleRequestId = Guid.NewGuid(), ManagerBy = actor };
        var team = new SampleRequest { SampleRequestId = Guid.NewGuid(), ManagerBy = subordinate };
        var readOnly = new SampleRequest { SampleRequestId = Guid.NewGuid(), ManagerBy = actor };
        var visible = new[] { own, team, readOnly }.AsQueryable();
        var writable = new[] { own, team }.AsQueryable();
        Guid[] allowed = [actor, subordinate];

        Assert.Equal(2, SampleRequestWorkQueueQuery.ApplySaleOwnership(visible, writable, allowed).Count());
        Assert.Equal(own.SampleRequestId, Assert.Single(SampleRequestWorkQueueQuery.ApplyMySaleOwnership(
            visible, writable, allowed, actor)).SampleRequestId);
        Assert.Empty(SampleRequestWorkQueueQuery.ApplyMySaleOwnership(visible, writable, allowed, null));
        Assert.Empty(SampleRequestWorkQueueQuery.ApplyMySaleOwnership(visible, writable, [subordinate], actor));
    }
}
