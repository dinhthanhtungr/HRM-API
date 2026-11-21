using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleSalesTeamVisibilityTests
{
    [Fact]
    public void AssignedCustomerExposesOutsideOwnerSampleButNotUnassignedOrForeignCustomers()
    {
        var company = Guid.NewGuid(); var employee = Guid.NewGuid(); var group = Guid.NewGuid();
        var assigned = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true, IsLead = false };
        assigned.CustomerAssignments.Add(new CustomerAssignment { CustomerId = assigned.CustomerId,
            GroupId = group, EmployeeId = Guid.NewGuid(), CompanyId = company, IsActive = true });
        var hidden = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true, IsLead = false };
        var visibleSample = Sample(company, Guid.NewGuid()); visibleSample.CustomerId = assigned.CustomerId;
        var hiddenSample = Sample(company, employee); hiddenSample.CustomerId = hidden.CustomerId;
        var foreign = Sample(Guid.NewGuid(), employee); foreign.CustomerId = assigned.CustomerId;
        var inactive = Sample(company, employee); inactive.CustomerId = assigned.CustomerId; inactive.IsActive = false;
        var scope = new ViewerScope(company, employee, false, false, new HashSet<Guid> { group },
            new HashSet<Guid> { employee }, DateTime.Now);
        var result = new CustomerVisibilityService(null!, null!, null!).ApplySampleRequestVisibility(
            new[] { visibleSample, hiddenSample, foreign, inactive }.AsQueryable(),
            new[] { assigned, hidden }.AsQueryable(), scope).ToArray();
        Assert.Equal(visibleSample.SampleRequestId, Assert.Single(result).SampleRequestId);
    }

    [Theory]
    [InlineData("SaleAdmin")]
    [InlineData("SaleUser")]
    public async Task CustomerReadIncludesSamplesOwnedOutsideManagedTeam(string role)
    {
        var user = new User(role, "CustomerViewAll");
        var team = Guid.NewGuid(); var otherTeam = Guid.NewGuid(); var inactiveTeam = Guid.NewGuid();
        var member = Guid.NewGuid(); var outsider = Guid.NewGuid(); var inactiveMember = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<ICustomerVisibilityReadDbContext>();
        store.Seed(nameof(db.MemberInGroups),
            Membership(user.EmployeeId!.Value, team, true), Membership(member, team),
            Membership(inactiveMember, team, active: false),
            Membership(user.EmployeeId.Value, otherTeam), Membership(outsider, otherTeam),
            Membership(user.EmployeeId.Value, inactiveTeam, true, false), Membership(outsider, inactiveTeam));
        var service = new CustomerVisibilityService(db, user, new Clock());
        var scope = await service.BuildReadScopeAsync();
        Assert.True(scope.HasFullCustomerView);
        Assert.Equal(new[] { member, user.EmployeeId.Value }.Order(), scope.EmployeeIdsInScope.Order());
        var own = Sample(user.CompanyId!.Value, user.EmployeeId.Value);
        var managed = Sample(user.CompanyId.Value, member);
        var outside = Sample(user.CompanyId.Value, outsider);
        var inactive = Sample(user.CompanyId.Value, user.EmployeeId.Value); inactive.IsActive = false;
        var foreign = Sample(Guid.NewGuid(), member);
        var rows = new[] { own, managed, outside, inactive, foreign, Sample(user.CompanyId.Value, inactiveMember) }.AsQueryable();
        Assert.Equal(rows.Where(x => x.CompanyId == user.CompanyId && x.IsActive).Select(x => x.SampleRequestId).Order(),
            service.ApplySampleRequestVisibility(rows, Array.Empty<Customer>().AsQueryable(), scope)
                .Select(x => x.SampleRequestId).Order());
        Assert.Equal(4, service.ApplySampleRequestVisibility(rows, Array.Empty<Customer>().AsQueryable(),
            scope with { HasFullCustomerView = true, CanViewInternalCustomer = true }).Count());
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task SaleAdminScopeStillRetainsOwnIdentityForWorkflowActions()
    {
        var user = new User("SaleAdmin", "CustomerViewAll");
        var (db, store) = ProductionOrderTestStore.CreateContext<ICustomerVisibilityReadDbContext>();
        store.Seed<MemberInGroup>(nameof(db.MemberInGroups));
        var scope = await new CustomerVisibilityService(db, user, new Clock()).BuildScopeAsync();
        Assert.Equal(new[] { user.EmployeeId!.Value }, scope.EmployeeIdsInScope);
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("LabUser")]
    [InlineData("President")]
    public async Task NonSupervisingRolesKeepExistingSampleVisibility(string role)
    {
        var user = new User(role);
        var (db, store) = ProductionOrderTestStore.CreateContext<ICustomerVisibilityReadDbContext>();
        store.Seed<MemberInGroup>(nameof(db.MemberInGroups));
        var scope = await new CustomerVisibilityService(db, user, new Clock()).BuildReadScopeAsync();
        Assert.Equal(user.CompanyId, scope.CompanyId);
    }

    [Fact]
    public void SampleReadSqlKeepsCompanyAndActiveWithoutOwnerPredicate()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var employee = Guid.NewGuid(); var member = Guid.NewGuid();
        var scope = new ViewerScope(Guid.NewGuid(), employee, true, false, new HashSet<Guid> { Guid.NewGuid() },
            new HashSet<Guid> { employee, member }, DateTime.Now);
        var query = new CustomerVisibilityService(null!, null!, null!).ApplySampleRequestVisibility(
            db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope);
        var sql = query.OrderBy(x => x.SampleRequestId).Skip(0).Take(20).ToQueryString();
        Assert.DoesNotContain("ANY", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("IsActive", sql);
    }

    private static MemberInGroup Membership(Guid employee, Guid group, bool leader = false, bool active = true)
        => new() { MemberId = Guid.NewGuid(), Profile = employee, GroupId = group, IsAdmin = leader, IsActive = active };
    private static SampleRequest Sample(Guid company, Guid owner)
        => new() { SampleRequestId = Guid.NewGuid(), CompanyId = company, ManagerBy = owner, IsActive = true };
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 4); }
    private sealed class User(params string[] roles) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role);
    }
}
