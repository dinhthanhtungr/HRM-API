using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleSaleWorkflowAccessTests
{
    [Theory]
    [InlineData("SaleAdmin", true)]
    [InlineData("SaleUser", false)]
    [InlineData("LabUser", false)]
    [InlineData("LabAdmin", false)]
    [InlineData("Developer", false)]
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
        => new(user.CompanyId!.Value, user.EmployeeId!.Value, false, false, new HashSet<Guid>(),
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
