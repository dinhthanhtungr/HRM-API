using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetFinishDashboard;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class FinishDashboardScopeTests
{
    [Theory]
    [InlineData("SaleUser", true, false)]
    [InlineData("President", true, true)]
    [InlineData("Developer", true, true)]
    [InlineData("Admin", false, false)]
    [InlineData("Leader", true, false)]
    [InlineData("SaleAdmin", false, false)]
    [InlineData("ACUser", false, false)]
    [InlineData("ACCUser", true, true)]
    [InlineData("PLPUUser", true, true)]
    [InlineData("DispatchUser", false, false)]
    [InlineData("QCUser", false, false)]
    public void ExactRoles(string role, bool read, bool company)
    {
        var user = new Viewer { Roles = [role] };
        Assert.Equal(read, FinishDashboardScope.CanRead(user));
        Assert.Equal(company, FinishDashboardScope.IsCompany(user));
    }

    [Theory]
    [InlineData("Leader")]
    [InlineData("CustomerViewAll")]
    [InlineData("Admin")]
    public void ExtraRolesCannotWidenSaleScope(string extra)
    {
        var user = new Viewer { Roles = ["SaleUser", extra] };
        var own = Customer(user.CompanyId!.Value);
        own.CustomerAssignments.Add(new() { CompanyId = user.CompanyId.Value, EmployeeId = user.EmployeeId!.Value });
        var other = Customer(user.CompanyId.Value);
        var foreign = Customer(Guid.NewGuid()); foreign.CurrentSaleId = user.EmployeeId;
        Assert.False(FinishDashboardScope.IsCompany(user));
        Assert.Equal(own.CustomerId, Assert.Single(FinishDashboardScope.Customers(new[] { own, other, foreign }.AsQueryable(), user, DateTime.UtcNow)).CustomerId);
    }

    [Theory]
    [InlineData("President")]
    [InlineData("Developer")]
    [InlineData("ACCUser")]
    [InlineData("PLPUUser")]
    public void FullViewStillCannotCrossCompany(string role)
    {
        var user = new Viewer { Roles = [role], EmployeeId = null };
        var own = Customer(user.CompanyId!.Value); own.IsActive = false;
        Assert.Equal(own.CustomerId, Assert.Single(FinishDashboardScope.Customers(new[] { own, Customer(Guid.NewGuid()) }.AsQueryable(), user, DateTime.UtcNow)).CustomerId);
    }

    [Fact]
    public void MissingContextAndAnonymousFailClosed()
    {
        var customers = new[] { Customer(Guid.NewGuid()) }.AsQueryable();
        Assert.False(FinishDashboardScope.CanRead(new Viewer { Roles = ["President"], IsAuthenticated = false }));
        Assert.Empty(FinishDashboardScope.Customers(customers, new Viewer { Roles = ["President"], CompanyId = null }, DateTime.UtcNow));
        Assert.Empty(FinishDashboardScope.Customers(customers, new Viewer { Roles = ["SaleUser"], EmployeeId = null }, DateTime.UtcNow));
    }

    [Fact]
    public void ActivePersonalAssignmentAndWorkClaimAllowOnlyTheirCustomers()
    {
        var user = new Viewer { Roles = ["SaleUser"] };
        var company = user.CompanyId!.Value;
        var now = DateTime.UtcNow;
        var assigned = Customer(company);
        assigned.CustomerAssignments.Add(new() { EmployeeId = user.EmployeeId!.Value, CompanyId = company });
        var claimed = Customer(company);
        claimed.CustomerClaims.Add(new() { EmployeeId = user.EmployeeId.Value, CompanyId = company, Type = ClaimType.Work, ExpiresAt = now.AddHours(1) });
        var expired = Customer(company);
        expired.CustomerClaims.Add(new() { EmployeeId = user.EmployeeId.Value, CompanyId = company, Type = ClaimType.Work, ExpiresAt = now.AddTicks(-1) });
        var foreignAssignment = Customer(company);
        foreignAssignment.CurrentSaleId = user.EmployeeId;
        foreignAssignment.CustomerAssignments.Add(new() { EmployeeId = user.EmployeeId.Value, CompanyId = Guid.NewGuid() });
        var customers = new[] { assigned, claimed, expired, foreignAssignment }.AsQueryable();
        Assert.Equal(2, FinishDashboardScope.Customers(customers, user, now).Count());
    }

    [Fact]
    public void LegacyMismatchedHeaderAndDhgCustomersNeverLeakIntoTotals()
    {
        var own = Guid.NewGuid(); var other = Guid.NewGuid();
        var rows = new[] {
            new DeliveryFinishReportSource { CustomerId = own, DeliveryCustomerId = own, Row = new() { QuantityKg = 12 } },
            new DeliveryFinishReportSource { CustomerId = own, DeliveryCustomerId = other, Row = new() { QuantityKg = 100 } },
            new DeliveryFinishReportSource { CustomerId = other, DeliveryCustomerId = own, Row = new() { QuantityKg = 200 } }
        }.AsQueryable();
        var scoped = GetFinishDashboardQueryHandler.ApplyCustomerScope(rows, new[] { own }.AsQueryable());
        Assert.Equal(12, GetFinishDashboardQueryHandler.Summary(scoped).Single().QuantityKg);
        Assert.Single(scoped);
    }

    [Fact]
    public void PersonalScopeAndTotalsTranslateToSqlBeforePaging()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new Viewer { Roles = ["SaleUser", "CustomerViewAll"] };
        var ids = FinishDashboardScope.Customers(db.Customers.AsNoTracking(), user, DateTime.UtcNow).Select(x => x.CustomerId);
        var source = DeliveryFinishReportQuery.Project(db.DeliveryOrders.AsNoTracking(), user.CompanyId!.Value, new(2026, 11, 1), new(2026, 11, 7));
        var sql = GetFinishDashboardQueryHandler.Summary(GetFinishDashboardQueryHandler.ApplyCustomerScope(source, ids)).ToQueryString();
        Assert.Contains(user.EmployeeId!.Value.ToString(), sql);
        Assert.Contains("GROUP BY", sql);
        Assert.DoesNotContain("UnitCostSnapshot", sql);
    }

    [Fact]
    public void LeaderTeamIsBoundedByManagedGroupsAndOwnToggle()
    {
        var user = new Viewer { Roles = ["Leader"] };
        var company = user.CompanyId!.Value;
        var group = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var own = Customer(company);
        own.CustomerAssignments.Add(new() { CompanyId = company, EmployeeId = user.EmployeeId!.Value });
        var team = Customer(company);
        team.CustomerAssignments.Add(new() { CompanyId = company, GroupId = group });
        var claim = Customer(company);
        claim.CustomerClaims.Add(new() { CompanyId = company, GroupId = group, Type = ClaimType.Work, ExpiresAt = now.AddDays(1) });
        var expired = Customer(company);
        expired.CustomerClaims.Add(new() { CompanyId = company, GroupId = group, Type = ClaimType.Work, ExpiresAt = now.AddDays(-1) });
        var other = Customer(company);
        other.CustomerAssignments.Add(new() { CompanyId = company, GroupId = Guid.NewGuid() });
        var foreign = Customer(Guid.NewGuid());
        foreign.CustomerAssignments.Add(new() { CompanyId = company, GroupId = group });
        var inactive = Customer(company); inactive.IsActive = false;
        inactive.CustomerAssignments.Add(new() { CompanyId = company, GroupId = group });
        var rows = new[] { own, team, claim, expired, other, foreign, inactive }.AsQueryable();
        var groups = new[] { group }.AsQueryable();
        Assert.Equal(3, FinishDashboardScope.Customers(rows, user, now, groups).Count());
        Assert.Equal(own.CustomerId, Assert.Single(FinishDashboardScope.Customers(rows, user, now, groups, ownOnly: true)).CustomerId);
        Assert.Single(FinishDashboardScope.Customers(rows, new Viewer { Roles = ["SaleUser"], EmployeeId = user.EmployeeId, CompanyId = company }, now, groups));
        Assert.Single(FinishDashboardScope.Customers(rows, user, now));
    }

    [Fact]
    public void TeamScopeTranslatesManagedGroupMembershipAndCompanyToSql()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var user = new Viewer { Roles = ["Leader"] };
        var groups = db.MemberInGroups.Where(x => x.IsActive && x.IsAdmin == true && x.Profile == user.EmployeeId && x.Group.CompanyId == user.CompanyId).Select(x => x.GroupId);
        var ids = FinishDashboardScope.Customers(db.Customers, user, DateTime.UtcNow, groups).Select(x => x.CustomerId);
        var source = DeliveryFinishReportQuery.Project(db.DeliveryOrders, user.CompanyId!.Value, new(2026, 11, 1), new(2026, 11, 7));
        var sql = GetFinishDashboardQueryHandler.Summary(GetFinishDashboardQueryHandler.ApplyCustomerScope(source, ids)).ToQueryString();
        Assert.Contains("IsAdmin", sql);
        Assert.Contains("GroupId", sql);
        Assert.Contains(user.CompanyId.Value.ToString(), sql);
        Assert.Contains("GROUP BY", sql);
    }

    private static Customer Customer(Guid company) => new() { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true };
    private sealed class Viewer : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; init; } = Guid.NewGuid();
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public bool IsInRole(string role) => Roles.Contains(role);
        public bool IsInAnyRole(params string[] roles) => roles.Any(IsInRole);
    }
}
