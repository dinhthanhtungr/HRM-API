using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleSaleWorkflowAccessTests
{
    [Fact]
    public void CancellationUsesCustomerAssignmentThenLiveLeadClaimThenLegacyOwner()
    {
        var user = new User(["SaleUser"]);
        var scope = Scope(user) with { HasFullCustomerView = true };
        var other = Guid.NewGuid();
        Customer Customer(string name) => new() { CustomerId = Guid.NewGuid(), CustomerName = name,
            CompanyId = scope.CompanyId, IsActive = true, CurrentSaleId = other };
        var assigned = Customer("assigned");
        assigned.CustomerAssignments.Add(new() { CompanyId = scope.CompanyId, IsActive = true, EmployeeId = scope.EmployeeId });
        var staleOwner = Customer("stale");
        staleOwner.CurrentSaleId = scope.EmployeeId;
        staleOwner.CustomerAssignments.Add(new() { CompanyId = scope.CompanyId, IsActive = true, EmployeeId = other });
        var lead = Customer("lead"); lead.IsLead = true;
        lead.CustomerClaims.Add(new() { CompanyId = scope.CompanyId, IsActive = true, Type = ClaimType.Work,
            ExpiresAt = scope.Now.AddDays(1), EmployeeId = scope.EmployeeId });
        var expired = Customer("expired"); expired.IsLead = true;
        expired.CustomerClaims.Add(new() { CompanyId = scope.CompanyId, IsActive = true, Type = ClaimType.Work,
            ExpiresAt = scope.Now.AddDays(-1), EmployeeId = scope.EmployeeId });
        var legacy = Customer("legacy"); legacy.CurrentSaleId = scope.EmployeeId;
        var foreign = Customer("foreign"); foreign.CompanyId = Guid.NewGuid(); foreign.CurrentSaleId = scope.EmployeeId;
        var inactive = Customer("inactive"); inactive.IsActive = false; inactive.CurrentSaleId = scope.EmployeeId;
        var query = new[] { assigned, staleOwner, lead, expired, legacy, foreign, inactive }.AsQueryable();
        var result = SampleSaleWorkflowAccess.CustomersAllowedForCancellation(query, user, new CurrentUserPermissionService(user), scope)
            .Select(c => c.CustomerId).ToArray();
        Assert.Equal(new[] { assigned.CustomerId, lead.CustomerId, legacy.CustomerId }, result);
    }

    [Fact]
    public void CustomerCancellationKeepsManagedScopeAndDoesNotGrantOtherWorkflowActions()
    {
        var user = new User(["SaleAdmin"]);
        var managed = Guid.NewGuid();
        var scope = Scope(user, managed) with { HasFullCustomerView = true };
        var owned = new Customer { CustomerId = Guid.NewGuid(), CompanyId = scope.CompanyId, IsActive = true, CurrentSaleId = managed };
        var outside = new Customer { CustomerId = Guid.NewGuid(), CompanyId = scope.CompanyId, IsActive = true, CurrentSaleId = Guid.NewGuid() };
        var rows = SampleSaleWorkflowAccess.CustomersAllowedForCancellation(new[] { owned, outside }.AsQueryable(), user,
            new CurrentUserPermissionService(user), scope).ToArray();
        Assert.Single(rows);
        Assert.Equal(owned.CustomerId, rows[0].CustomerId);
        Assert.False(Can(user, outside.CurrentSaleId, scope));
        Assert.Empty(SampleSaleWorkflowAccess.CustomersAllowedForCancellation(new[] { owned }.AsQueryable(), user,
            new CurrentUserPermissionService(user), scope with { CompanyId = Guid.NewGuid() }));
    }

    [Fact]
    public void CustomerCancellationQueryTranslatesWithoutOpeningDatabase()
    {
        var user = new User(["SaleUser"]);
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = SampleSaleWorkflowAccess.CustomersAllowedForCancellation(db.Customers, user,
            new CurrentUserPermissionService(user), Scope(user)).ToQueryString();
        Assert.Contains(db.Model.FindEntityType(typeof(CustomerAssignment))!.GetTableName()!, sql);
        Assert.Contains(db.Model.FindEntityType(typeof(CustomerClaim))!.GetTableName()!, sql);
    }
    [Theory]
    [InlineData("SaleAdmin", true)]
    [InlineData("SaleUser", true)]
    [InlineData("LabUser", false)]
    [InlineData("LabAdmin", false)]
    [InlineData("Developer", true)]
    [InlineData("President", false)]
    public void OnlyDelegationCapabilityCanActForManagedSale(string role, bool allowed)
    {
        var user = new User([role]);
        var owner = Guid.NewGuid();
        Assert.Equal(allowed, Can(user, owner, Scope(user, owner)));
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("SaleAdmin")]
    public void OwnRequestsRemainAvailable(string role)
    {
        var user = new User([role]);
        Assert.True(Can(user, user.EmployeeId, Scope(user)));
    }

    [Fact]
    public void BroadReadVisibilityDoesNotGrantDelegationOutsideManagedOwners()
    {
        var user = new User(["SaleAdmin"]);
        Assert.False(Can(user, Guid.NewGuid(), Scope(user) with { HasFullCustomerView = true }));
    }

    [Fact]
    public void OrdinarySaleUserCannotDelegateWithoutARealLeaderGroupEvenWithManagedCapability()
    {
        var user = new User(["SaleUser"]);
        var owner = Guid.NewGuid();
        Assert.False(Can(user, owner, Scope(user, owner) with { LeaderGroupIds = new HashSet<Guid>() }));
        Assert.True(Can(user, user.EmployeeId, Scope(user)));
    }

    [Fact]
    public void ExplicitClaimsOverrideRoleFallbackAndRequireBothCapabilities()
    {
        var owner = Guid.NewGuid();
        var denied = new User(["SaleAdmin"], [], true);
        Assert.False(Can(denied, owner, Scope(denied, owner)));
        var baseOnly = new User(["SaleAdmin"], [ApplicationPermissions.PLM.ManageSampleSaleWorkflow], true);
        Assert.True(Can(baseOnly, baseOnly.EmployeeId, Scope(baseOnly)));
        Assert.False(Can(baseOnly, owner, Scope(baseOnly, owner)));
        var delegatedOnly = new User(["SaleAdmin"], [ApplicationPermissions.PLM.ManageManagedSampleSaleWorkflow], true);
        Assert.False(Can(delegatedOnly, owner, Scope(delegatedOnly, owner)));
        var granted = new User([], [ApplicationPermissions.PLM.ManageSampleSaleWorkflow,
            ApplicationPermissions.PLM.ManageManagedSampleSaleWorkflow], true);
        Assert.True(Can(granted, owner, Scope(granted, owner)));
    }

    [Fact]
    public void InvalidAuthenticationOrScopeFailsClosed()
    {
        var user = new User(["SaleAdmin"]);
        var scope = Scope(user);
        Assert.False(Can(user, user.EmployeeId, scope with { CompanyId = Guid.NewGuid() }));
        Assert.False(Can(user, user.EmployeeId, scope with { EmployeeId = Guid.NewGuid() }));
        Assert.False(Can(user, Guid.Empty, scope));
        Assert.False(Can(user, null, scope));
        user.Authenticated = false;
        Assert.False(Can(user, user.EmployeeId, scope));
    }

    [Fact]
    public void DeveloperCanActAcrossOwnersButNeverAcrossCompanyOrInvalidIdentity()
    {
        var user = new User([ApplicationRoles.Developer], [], true);
        var scope = Scope(user);
        var permissions = new CurrentUserPermissionService(user);
        foreach (var permission in new[] { ApplicationPermissions.PLM.ManageSampleSaleWorkflow,
            ApplicationPermissions.PLM.ManageSampleLabWorkflow, ApplicationPermissions.PLM.ConfirmCustomerPrice,
            ApplicationPermissions.PLM.CorrectSampleTrialFeedback })
            Assert.True(permissions.HasPermission(permission));
        Assert.True(Can(user, Guid.NewGuid(), scope));
        Assert.False(Can(user, Guid.NewGuid(), scope with { CompanyId = Guid.NewGuid() }));
        Assert.False(Can(user, Guid.NewGuid(), scope with { EmployeeId = Guid.NewGuid() }));
        user.Authenticated = false;
        Assert.False(Can(user, Guid.NewGuid(), scope));
        Assert.False(permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow));
    }

    [Fact]
    public void SamplePermissionsDoNotExpandUnrelatedFormulaSelectors()
    {
        Assert.DoesNotContain("SaleAdmin", ApplicationRoleSets.PLM.FormulaSelectors);
        Assert.Contains("SaleAdmin", ApplicationPermissionRoleSets.GetRoles(ApplicationPermissions.PLM.ConfirmCustomerPrice));
        Assert.Contains("SaleAdmin", ApplicationPermissionRoleSets.GetRoles(ApplicationPermissions.PLM.CorrectSampleTrialFeedback));
        Assert.Contains(ApplicationPermissions.PLM.ManageManagedSampleSaleWorkflow, ApplicationPermissionCatalog.Codes);
    }

    private static bool Can(User user, Guid? owner, ViewerScope scope)
        => SampleSaleWorkflowAccess.CanActFor(user, new CurrentUserPermissionService(user), owner, scope);
    private static ViewerScope Scope(User user, params Guid[] owners)
        => new(user.CompanyId!.Value, user.EmployeeId!.Value, false, false,
            owners.Length > 0 ? new HashSet<Guid> { Guid.NewGuid() } : new HashSet<Guid>(),
            owners.Append(user.EmployeeId.Value).ToHashSet(), DateTime.Now);

    private sealed class User(string[] roles, string[]? permissions = null, bool explicitSet = false) : ICurrentUser
    {
        public bool Authenticated { get; set; } = true;
        public bool IsAuthenticated => Authenticated;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => roles;
        public IReadOnlyCollection<string> Permissions => permissions ?? [];
        public bool HasExplicitPermissionSet => explicitSet;
        public bool IsInRole(string role) => roles.Contains(role);
    }
}
