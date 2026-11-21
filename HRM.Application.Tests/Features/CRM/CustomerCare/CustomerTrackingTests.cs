using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.CRM.CustomerCare;

public sealed class CustomerTrackingTests
{
    [Fact]
    public void InteractionProjectionDoesNotReadFreeTextOrReferences()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = ListCustomerTrackingInteractionsQueryHandler.Project(db.CustomerInteractions).ToQueryString();
        Assert.Contains("InteractionAt", sql);
        Assert.Contains("NextFollowUpDate", sql);
        Assert.DoesNotContain("\"Subject\"", sql);
        Assert.DoesNotContain("\"Content\"", sql);
        Assert.DoesNotContain("\"Outcome\"", sql);
        Assert.DoesNotContain("\"NextAction\"", sql);
        Assert.DoesNotContain("Reference", sql);
    }

    [Fact]
    public void TrackingRequiresItsOwnPermissionAndProjectsOnlyCompanyCustomerAndAssignee()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new User();
        var reader = new CustomerTrackingReader(db, user, new CurrentUserPermissionService(user), new Clock());
        var sql = reader.Project(reader.Read()!).Take(25).ToQueryString();
        Assert.Contains("CompanyId", sql);
        Assert.Contains("FullName", sql);
        Assert.DoesNotContain("\"Note\"", sql);
        Assert.DoesNotContain("\"Phone\"", sql);
        Assert.DoesNotContain("\"TaxCode\"", sql);

        var withoutClaim = user with { HasExplicitPermissionSet = true };
        var denied = new CustomerTrackingReader(db, withoutClaim, new CurrentUserPermissionService(withoutClaim), new Clock());
        Assert.Null(denied.Read());
    }

    private sealed class Clock : IDateTimeProvider
    {
        public DateTime Now => new(2026, 11, 20);
    }

    private sealed record User : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public bool HasExplicitPermissionSet { get; init; }
        public IReadOnlyCollection<string> Roles { get; } =
            [ApplicationRoles.InternalSystems.IMSUser, ApplicationRoles.Accounting.ACCUser];
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
