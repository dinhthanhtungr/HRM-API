using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Commands.RetryWorkflowNotificationJob;
using HRM.Application.Features.PLM.SampleRequests.Commands.RestoreWorkflowSummary;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetWorkflowNotificationJobs;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class WorkflowNotificationJobAdminTests
{
    [Theory]
    [InlineData(ApplicationRoles.Developer, true)]
    [InlineData(ApplicationRoles.President, false)]
    [InlineData(ApplicationRoles.Admin, false)]
    [InlineData(ApplicationRoles.Lab.LabUser, false)]
    [InlineData(ApplicationRoles.Sales.SaleUser, false)]
    public void CapabilityBaselineIsDeveloperOnly(string role, bool expected)
    {
        var permissions = new CurrentUserPermissionService(new User { Roles = [role] });
        Assert.Equal(expected, permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs));
        Assert.Equal(expected, permissions.HasPermission(ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs));
        Assert.False(permissions.HasPermission("unknown.permission"));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task MissingContextIsDeniedBeforeAnyDatabaseAccess(bool authenticated, bool company, bool employee)
    {
        var user = new User { IsAuthenticated = authenticated, CompanyId = company ? Guid.NewGuid() : null,
            EmployeeId = employee ? Guid.NewGuid() : null, Roles = [ApplicationRoles.Developer] };
        var permission = new CurrentUserPermissionService(user);
        var read = new GetWorkflowNotificationJobsQueryHandler(null!, user, permission);
        var retry = new RetryWorkflowNotificationJobCommandHandler(null!, user, permission,
            NullLogger<RetryWorkflowNotificationJobCommandHandler>.Instance);
        Assert.Null(await read.Handle(new(), default));
        Assert.Equal("Forbidden", (await retry.Handle(new(1), default)).Outcome);
        Assert.Equal("Forbidden", (await new RestoreWorkflowSummaryCommandHandler(null!, user, permission,
            NullLogger<RestoreWorkflowSummaryCommandHandler>.Instance, null!).Handle(new(1), default)).Outcome);
    }

    [Fact]
    public async Task UnprivilegedUserCannotReadOrRetryEvenWithValidIds()
    {
        var user = new User { Roles = [ApplicationRoles.President] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.Null(await new GetWorkflowNotificationJobsQueryHandler(null!, user, permissions).Handle(new(), default));
        Assert.Equal("Forbidden", (await new RetryWorkflowNotificationJobCommandHandler(null!, user, permissions,
            NullLogger<RetryWorkflowNotificationJobCommandHandler>.Instance).Handle(new(99), default)).Outcome);
        Assert.Equal("Forbidden", (await new RestoreWorkflowSummaryCommandHandler(null!, user, permissions,
            NullLogger<RestoreWorkflowSummaryCommandHandler>.Instance, null!).Handle(new(99), default)).Outcome);
    }

    [Fact]
    public void DeveloperExplicitTokenRequiresNewPermissionClaims()
    {
        var oldToken = new User { Roles = [ApplicationRoles.Developer], HasExplicitPermissionSet = true };
        Assert.False(new CurrentUserPermissionService(oldToken).HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs));
        var refreshed = oldToken with { Permissions = [ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs,
            ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs] };
        var permissions = new CurrentUserPermissionService(refreshed);
        Assert.True(permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs));
        Assert.True(permissions.HasPermission(ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs));
    }

    [Fact]
    public void ExplicitClaimsCanSeparateReadAndRetryAndAnonymousClaimsCannotGrantAccess()
    {
        var user = new User { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs] };
        var permission = new CurrentUserPermissionService(user);
        Assert.True(permission.HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs));
        Assert.False(permission.HasPermission(ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs));
        Assert.False(new CurrentUserPermissionService(user with { IsAuthenticated = false }).HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs));
    }

    [Theory]
    [InlineData(false, 0, 1, "Pending", false)]
    [InlineData(false, 4, 11, "Delayed", false)]
    [InlineData(false, 5, 1, "Failed", true)]
    [InlineData(false, 9, 11, "Failed", true)]
    [InlineData(true, 5, 11, "Completed", false)]
    public void StatusAndRetryPreventCompletedOrAlreadyQueuedReplay(bool completed, int attempts, int minutes, string status, bool retry)
    {
        var now = new DateTime(2026, 11, 3, 12, 0, 0);
        DateTime? processed = completed ? now : null;
        Assert.Equal(status, WorkflowNotificationJobRules.Status(processed, attempts, now.AddMinutes(-minutes), now));
        Assert.Equal(retry, WorkflowNotificationJobRules.CanRetry(processed, attempts, null));
        Assert.False(WorkflowNotificationJobRules.CanRetry(processed, attempts, Guid.NewGuid()));
    }

    [Fact]
    public void SafeErrorsNeverExposeLegacyPayloadOrExceptionText()
    {
        Assert.Null(WorkflowNotificationJobRules.SafeError(null));
        Assert.Equal("WorkflowNotification.MissingPresident", WorkflowNotificationJobRules.SafeError("WorkflowNotification.MissingPresident"));
        Assert.Equal("WorkflowNotification.Unknown", WorkflowNotificationJobRules.SafeError("private customer body"));
        Assert.Equal("WorkflowNotification.Unknown", WorkflowNotificationJobRules.SafeError("WorkflowNotification.99999"));
        Assert.Equal("WorkflowNotification.LegacyHandlerMissing", WorkflowNotificationJobRules.SafeError("No handler for type SampleRequestWorkflowNotification"));
    }

    [Fact]
    public void MissingHandlerRecoveryRequiresExactFailureAndNoPublicationEvidence()
    {
        var now = DateTime.Now;
        const string error = "No handler for type SampleRequestWorkflowNotification";
        Assert.True(WorkflowNotificationJobRules.CanRecoverMissingHandler(now, error, null, null));
        Assert.False(WorkflowNotificationJobRules.CanRecoverMissingHandler(null, error, null, null));
        Assert.False(WorkflowNotificationJobRules.CanRecoverMissingHandler(now, null, null, null));
        Assert.False(WorkflowNotificationJobRules.CanRecoverMissingHandler(now, "other failure", null, null));
        Assert.False(WorkflowNotificationJobRules.CanRecoverMissingHandler(now, error, Guid.NewGuid(), null));
        Assert.False(WorkflowNotificationJobRules.CanRecoverMissingHandler(now, error, null, Guid.NewGuid()));
    }

    [Fact]
    public void ProcessedWithLegacyErrorIsNotSuccessfulAndCannotBeBlindlyRetried()
    {
        var now = new DateTime(2026, 11, 3, 12, 0, 0);
        Assert.Equal("NeedsReview", WorkflowNotificationJobRules.Status(now, 1, now.AddDays(-1), now,
            "No handler for type SampleRequestWorkflowNotification"));
        Assert.False(WorkflowNotificationJobRules.CanRetry(now, 1, null));
    }

    [Fact]
    public void ListAndRetrySqlScopePayloadEventAndSampleAndUseWorkerCompatibleLock()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var list = WorkflowNotificationJobQueries.Scoped(db.OutboxMessages, company, "TP_31075")
            .Where(x => x.ProcessedAt == null).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(20).Take(20).ToQueryString();
        var retry = WorkflowNotificationJobQueries.LockScoped(db.OutboxMessages, company, 123).ToQueryString();
        foreach (var sql in new[] { list, retry })
        {
            Assert.Contains("payload_json->>'CompanyId'", sql);
            Assert.Contains("e.\"CompanyId\"=", sql);
            Assert.Contains("s.\"CompanyId\"=", sql);
            Assert.Contains(company.ToString(), sql);
            Assert.Contains("e.\"Id\"::text=o.payload_json->>'EventId'", sql);
        }
        Assert.Contains("LIMIT", list);
        Assert.Contains("FOR UPDATE OF o SKIP LOCKED", retry);
        Assert.DoesNotContain("INSERT", retry);
    }

    private sealed record User : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; init; } = Guid.NewGuid();
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => "job-admin-test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
