using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Commands.SetLegacyWorkTracking;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestWorkScopeTests
{
    [Fact]
    public void CutoffIsInclusiveAndDelayDoesNotIncludeLegacyWork()
    {
        var old = Request(new DateTime(2026, 10, 31, 23, 59, 59));
        old.IsDelayed = true;
        var current = Request(new DateTime(2026, 11, 1));
        Assert.Equal([current], SampleRequestWorkScope.Apply(new[] { old, current }.AsQueryable(),
            Array.Empty<SampleRequestWorkflowEvent>().AsQueryable()).ToArray());
    }

    [Fact]
    public void LatestTrackingChoiceWinsAndOtherCompanyCannotIncludeRequest()
    {
        var old = Request(new DateTime(2026, 10, 1));
        var include = Event(old, SampleRequestWorkScope.LegacyIncluded, 1);
        var exclude = Event(old, SampleRequestWorkScope.LegacyExcluded, 2);
        Assert.Single(SampleRequestWorkScope.Apply(new[] { old }.AsQueryable(), new[] { include }.AsQueryable()));
        Assert.Empty(SampleRequestWorkScope.Apply(new[] { old }.AsQueryable(), new[] { exclude, include }.AsQueryable()));
        include.CompanyId = Guid.NewGuid();
        Assert.Empty(SampleRequestWorkScope.Apply(new[] { old }.AsQueryable(), new[] { include }.AsQueryable()));
    }

    [Theory]
    [InlineData(2026, 12)]
    [InlineData(2027, 1)]
    public void UnfinishedNovemberWorkCarriesForwardAndClosedWorkLeavesActionQueue(int year, int month)
    {
        var pending = Request(new DateTime(2026, 11, 1));
        var completed = Request(new DateTime(2026, 11, 2)); completed.Status = "Completed";
        var cancelled = Request(new DateTime(2026, 11, 3)); cancelled.Status = "Cancelled";
        var scoped = SampleRequestWorkScope.Apply(new[] { pending, completed, cancelled }.AsQueryable(),
            Array.Empty<SampleRequestWorkflowEvent>().AsQueryable());
        var rows = scoped.Select(x => new SampleRequestSampleTrialReportRow
        {
            SampleRequest = x,
            WorkDecision = HRM.Application.Features.PLM.SampleRequests.Services.SampleWorkDecisionRules.Resolve(
                new HRM.Application.Features.PLM.SampleRequests.Services.SampleWorkFacts
                { Open = x.Status != "Completed" && x.Status != "Cancelled" })
        });
        var query = new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(year, month, 1),
            WorkAudience = SampleRequestWorkAudience.Lab, WorkBucket = SampleRequestWorkBucket.ActionRequired };
        Assert.Same(pending, Assert.Single(SampleRequestWorkQueueQuery.Apply(rows, query, null!)).SampleRequest);
        query.WorkBucket = SampleRequestWorkBucket.Completed;
        Assert.Same(completed, Assert.Single(SampleRequestWorkQueueQuery.Apply(rows, query, null!)).SampleRequest);
        query.WorkBucket = SampleRequestWorkBucket.Cancelled;
        Assert.Same(cancelled, Assert.Single(SampleRequestWorkQueueQuery.Apply(rows, query, null!)).SampleRequest);
        query.WorkBucket = SampleRequestWorkBucket.All;
        Assert.Equal(3, SampleRequestWorkQueueQuery.Apply(rows, query, null!).Count());
    }

    [Fact]
    public void ScopeTranslatesToPostgresBeforePaging()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var sql = SampleRequestWorkScope.Apply(db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive),
            db.SampleRequestWorkflowEvents).OrderBy(x => x.CreatedDate).Take(15).ToQueryString();
        Assert.Contains("LIMIT", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("LegacyWorkTrackingIncluded", sql);
    }

    [Theory]
    [InlineData(true, "Admin", true)]
    [InlineData(true, "LabAdmin", false)]
    [InlineData(true, "SaleUser", false)]
    [InlineData(true, "Developer", false)]
    [InlineData(false, "Admin", false)]
    public async Task TrackingRequiresItsOwnCapability(bool authenticated, string role, bool allowed)
    {
        var user = new User(authenticated, role);
        var permissions = new CurrentUserPermissionService(user);
        Assert.Equal(allowed, permissions.HasPermission(ApplicationPermissions.PLM.ManageLegacySampleWork));
        Assert.False(permissions.HasPermission("unknown.permission"));
        if (!allowed)
        {
            var handler = new SetLegacyWorkTrackingCommandHandler(null!, user, permissions, null!, null!);
            Assert.False((await handler.Handle(new SetLegacyWorkTrackingCommand(), CancellationToken.None)).Success);
        }
    }

    private static SampleRequest Request(DateTime created) => new() { SampleRequestId = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(), CreatedDate = created, Status = "InProgress", InfoType = "Thẻ màu" };
    private static SampleRequestWorkflowEvent Event(SampleRequest request, string type, int day) => new() {
        SampleRequestId = request.SampleRequestId, CompanyId = request.CompanyId,
        EventType = type, RecordedAt = new DateTime(2026, 11, day) };
    private sealed class User(bool authenticated, string role) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "work-scope-test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string candidate) => role == candidate;
    }
}
