using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestRoleWorkReportTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SharedReportingRejectsAnonymousOrMissingContextBeforeStorage(bool authenticated, bool hasContext)
    {
        var user = new User("SaleUser", authenticated, hasContext);
        Assert.False(SampleRequestReportAccess.CanRead(user));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetSampleRequestAdminSummaryQueryHandler(null!, user, null!, null!).Handle(new(), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetSampleRequestAdminDetailsQueryHandler(user, null!, null!, null!).Handle(new(), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleActivityDetails.GetSampleActivityDetailsQueryHandler(null!, null!, user, null!).Handle(new(), CancellationToken.None));
    }
    [Fact]
    public void ManualPinDoesNotHideSaleFeedbackActionOnNewRoleScreens()
    {
        var employee = Guid.NewGuid();
        var item = new SampleRequestSampleTrialReportDto { SampleRequestStatus = "SampleSent", IsDelayed = true,
            Status = SampleTrialStatus.WaitingCustomerFeedback, ManagerSalesEmployeeId = employee };
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 11, 1), false, false, false, true, employee,
            useDelayReviewAction: false);
        Assert.Equal("FollowUpCustomer", item.NextActionCode);
        Assert.Equal("Sale", item.ResponsibleRole);
        Assert.True(item.IsActionForCurrentUser);
        Assert.True(item.IsDelayed);
    }

    [Fact]
    public void ManualPinOfOtherwiseFinishedOpenQuotationGetsLabReviewAction()
    {
        var item = new SampleRequestSampleTrialReportDto { SampleRequestStatus = "InProgress", IsDelayed = true,
            InfoType = "Báo giá", LabFormulaConfirmedAt = new DateTime(2026, 11, 1), RealPriceQuoteDate = new DateTime(2026, 11, 1) };
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 11, 1), true, true, false, false, Guid.NewGuid(),
            useDelayReviewAction: false);
        Assert.Equal("ReviewDelayedWork", item.WorkStatus);
        Assert.Equal("ReviewSample", item.NextActionCode);
        Assert.Equal("Lab", item.ResponsibleRole);
        Assert.True(item.IsActionForCurrentUser);
        item.SampleRequestStatus = "Completed";
        SampleRequestDailyWorkRules.Apply(item, new DateOnly(2026, 11, 1), true, true, false, false, Guid.NewGuid(),
            useDelayReviewAction: false);
        Assert.Equal(string.Empty, item.NextActionCode);
    }

    [Theory]
    [InlineData("SaleUser", SampleRequestWorkAudience.Sale, true)]
    [InlineData("SaleAdmin", SampleRequestWorkAudience.Sale, true)]
    [InlineData("SaleAdmin", SampleRequestWorkAudience.Lab, false)]
    [InlineData("LabUser", SampleRequestWorkAudience.Sale, false)]
    [InlineData("LabAdmin", SampleRequestWorkAudience.Lab, true)]
    [InlineData("SaleUser", SampleRequestWorkAudience.Lab, false)]
    [InlineData("Admin", SampleRequestWorkAudience.Lab, true)]
    [InlineData("Developer", SampleRequestWorkAudience.Sale, false)]
    [InlineData("Developer", SampleRequestWorkAudience.Lab, true)]
    [InlineData("Developer", SampleRequestWorkAudience.LabAdmin, true)]
    [InlineData("Leader", SampleRequestWorkAudience.Sale, false)]
    public void NewAudienceDoesNotGrantOtherRoles(string role, SampleRequestWorkAudience audience, bool expected)
        => Assert.Equal(expected, SampleRequestWorkQueueAccess.CanRead(new User(role), audience));

    [Theory]
    [InlineData("Admin")]
    [InlineData("LabAdmin")]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    [InlineData("KHOUser")]
    [InlineData("HC_HRUser")]
    public void SummaryIsReadableAcrossRolesWithoutGrantingMutations(string role)
    {
        Assert.True(SampleRequestReportAccess.CanRead(new User(role)));
        Assert.Contains(ApplicationRoles.Admin, ApplicationRoleSets.PLM.SampleRequestLabProgressEditors);
        Assert.DoesNotContain(ApplicationRoles.Sales.SaleUser, ApplicationRoleSets.PLM.SampleRequestLabProgressEditors);
    }

    [Fact]
    public void PeriodCountsRequestsRatherThanTrialsAndExcludesUnknownTerminalDatesFromOpen()
    {
        var start = new DateTime(2026, 10, 1);
        var end = start.AddMonths(1);
        var rows = new[]
        {
            new SampleRequestAdminReportRecord { SampleRequestId = Guid.NewGuid(), CreatedDate = start.AddMonths(-1), Status = "InProgress" },
            new SampleRequestAdminReportRecord { SampleRequestId = Guid.NewGuid(), CreatedDate = start, Status = "Completed", CompletedDate = start.AddDays(2), LabCompletedDate = start.AddDays(2) },
            new SampleRequestAdminReportRecord { SampleRequestId = Guid.NewGuid(), CreatedDate = start, Status = "Cancelled", CancelledDate = start.AddDays(3) },
            new SampleRequestAdminReportRecord { SampleRequestId = Guid.NewGuid(), CreatedDate = start, Status = "Completed", CompletedDate = end.AddDays(2) },
            new SampleRequestAdminReportRecord { SampleRequestId = Guid.NewGuid(), CreatedDate = start, Status = "Cancelled" },
            new SampleRequestAdminReportRecord { SampleRequestId = Guid.NewGuid(), CreatedDate = end, Status = "New" }
        }.AsQueryable();
        Assert.Equal(2, SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.Open, start, end).Count());
        Assert.Single(SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.Completed, start, end));
        Assert.Single(SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.Cancelled, start, end));
    }

    [Fact]
    public void FormulaMetricsSeparateUnstartedAndActiveWorkWithoutCountingSentOrTerminalRequests()
    {
        var start = new DateTime(2026, 11, 1);
        var end = start.AddMonths(1);
        var rows = new[]
        {
            new SampleRequestAdminReportRecord { CreatedDate = start.AddMonths(-1), Status = "InProgress", HasFormula = true },
            new SampleRequestAdminReportRecord { CreatedDate = start, Status = "InProgress", HasFormula = false },
            new SampleRequestAdminReportRecord { CreatedDate = start, Status = "New", HasFormula = false },
            new SampleRequestAdminReportRecord { CreatedDate = start, Status = "SampleSent", HasFormula = true, LabCompletedDate = start.AddDays(1) },
            new SampleRequestAdminReportRecord { CreatedDate = start, Status = "Completed", HasFormula = true },
            new SampleRequestAdminReportRecord { CreatedDate = start, Status = "Cancelled", HasFormula = false },
            new SampleRequestAdminReportRecord { CreatedDate = end, Status = "InProgress", HasFormula = true }
        }.AsQueryable();
        Assert.Single(SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.InProgress, start, end));
        Assert.Equal(2, SampleRequestAdminReportQuery.Filter(rows, SampleRequestAdminMetric.Pending, start, end).Count());
    }

    [Fact]
    public void LabActionRequiredDoesNotReapplyMonthAfterScopeFiltering()
    {
        var date = new DateTime(2026, 11, 1);
        var rows = new[]
        {
            Row(date, false), Row(date.AddMonths(-1), false), Row(date.AddMonths(-1), true),
            new SampleRequestSampleTrialReportRow { SampleRequest = new SampleRequest { Status = "Completed", IsDelayed = true, CreatedDate = date } }
        }.AsQueryable();
        var query = new GetSampleRequestSampleTrialsQuery { DailyWorkDate = DateOnly.FromDateTime(date),
            WorkAudience = SampleRequestWorkAudience.Lab, WorkBucket = SampleRequestWorkBucket.ActionRequired };
        Assert.Equal(3, SampleRequestWorkQueueQuery.Apply(rows, query, null!).Count());
    }

    [Theory]
    [InlineData(SampleRequestWorkBucket.ActionRequired)]
    [InlineData(SampleRequestWorkBucket.WaitingPrice)]
    [InlineData(SampleRequestWorkBucket.WaitingLab)]
    public void SaleQueuesTranslateBeforePagination(SampleRequestWorkBucket bucket)
    {
        using var db = Db();
        var company = Guid.NewGuid();
        var rows = from item in db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive)
                   join trial in db.SampleRequestSampleTrials.Where(x => x.IsActive)
                       on item.SampleRequestId equals trial.SampleRequestId into trials
                   from trial in trials.DefaultIfEmpty()
                   select new SampleRequestSampleTrialReportRow { SampleRequest = item, Trial = trial, TrialCount = null };
        rows = SampleRequestLabWorkQuery.Enrich(rows, db);
        rows = HRM.Application.Features.PLM.SampleRequests.Services.SampleWorkDecisionQuery.Enrich(rows, db, db, "VND");
        var query = new GetSampleRequestSampleTrialsQuery { DailyWorkDate = new DateOnly(2026, 11, 1),
            WorkAudience = SampleRequestWorkAudience.Sale, WorkBucket = bucket };
        var sql = SampleRequestWorkQueueQuery.Apply(rows, query, db).Take(15).ToQueryString();
        Assert.Contains("LIMIT", sql);
        Assert.Contains("CompanyId", sql);
    }

    [Fact]
    public void AdminProjectionAndAllMetricsTranslateWithScopedMilestones()
    {
        using var db = Db();
        var company = Guid.NewGuid();
        var source = SampleRequestAdminReportQuery.Build(db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive), db, db.ProductPricingVersions);
        foreach (var metric in Enum.GetValues<SampleRequestAdminMetric>())
        {
            var sql = SampleRequestAdminReportQuery.Filter(source, metric, new DateTime(2026, 10, 1), new DateTime(2026, 11, 1))
                .GroupBy(x => x.BranchId).Select(x => new { Id = x.Key, Count = x.Count() }).ToQueryString();
            Assert.Contains("CompanyId", sql);
            Assert.Contains("GROUP BY", sql);
            Assert.Contains("LegacyWorkTrackingIncluded", sql);
            Assert.Contains("LegacyWorkTrackingExcluded", sql);
            // Completion submetrics share the same persisted rework/reset rules as Completed.
            if (metric is not (SampleRequestAdminMetric.Completed or SampleRequestAdminMetric.InProgress or
                SampleRequestAdminMetric.CompletedQuotation or SampleRequestAdminMetric.CompletedChip or
                SampleRequestAdminMetric.CompletedSample or SampleRequestAdminMetric.CompletedOther))
                Assert.DoesNotContain("UpdatedDate", sql);
        }
        Assert.False(SampleRequestAdminReportQuery.IsValidPeriod(new DateOnly(2026, 11, 1), DateOnly.MaxValue));
        Assert.False(SampleRequestAdminReportQuery.IsValidPeriod(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 1)));
    }

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
    private static SampleRequestSampleTrialReportRow Row(DateTime created, bool delayed) => new()
    {
        WorkDecision = new("PrepareChip", "ApproveFormula", "Lab"),
        SampleRequest = new SampleRequest { CreatedDate = created, IsDelayed = delayed, Status = "InProgress", InfoType = "Thẻ màu" }
    };
    private sealed class User(string role, bool authenticated = true, bool hasContext = true) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = hasContext ? Guid.NewGuid() : null;
        public Guid? CompanyId { get; } = hasContext ? Guid.NewGuid() : null;
        public string? UserName => "report-test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string candidate) => candidate == role;
    }
}
