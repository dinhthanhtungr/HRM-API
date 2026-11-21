using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Tests.Commons.Authorization;

public sealed class DatabaseBackedPermissionResolutionTests
{
    [Theory]
    [InlineData("ACCUser", true)]
    [InlineData("ACCUser", false)]
    [InlineData("PriceView", false)]
    [InlineData("Admin", false)]
    [InlineData("LabAdmin", true)]
    [InlineData("LabAdmin", false)]
    [InlineData("Developer", true)]
    public void ImsRestrictionTakesPrecedenceOverOtherRolesAndClaims(string otherRole, bool explicitPermissions)
    {
        var service = new CurrentUserPermissionService(new ExplicitPermissionCurrentUser(
            [ApplicationRoles.InternalSystems.IMSUser, otherRole],
            [
                ApplicationPermissions.Pricing.ViewApprovedSellingPrice,
                ApplicationPermissions.PLM.ViewFormulaMaterials,
                ApplicationPermissions.PLM.ConfirmCustomerPrice,
                ApplicationPermissions.Equipment.View,
            ], explicitPermissions));

        Assert.False(service.HasPermission(ApplicationPermissions.Pricing.ViewApprovedSellingPrice));
        Assert.False(service.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials));
        Assert.False(service.HasPermission(ApplicationPermissions.PLM.ConfirmCustomerPrice));
        if (explicitPermissions)
            Assert.True(service.HasPermission(ApplicationPermissions.Equipment.View));
    }

    [Theory]
    [InlineData("LabUser", true)]
    [InlineData("LabAdmin", true)]
    [InlineData("SaleUser", false)]
    public void LabDatesAndReceiptWorkWithOlderExplicitToken(string role, bool allowed)
    {
        var service = new CurrentUserPermissionService(new ExplicitPermissionCurrentUser([role], []));
        Assert.Equal(allowed, service.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule));
        Assert.Equal(allowed, service.HasPermission(ApplicationPermissions.PLM.ReceiveLabStandard));
        Assert.False(service.HasPermission(ApplicationPermissions.Pricing.Manage));
    }

    [Fact]
    public void DeveloperCanConfirmQuotationWithOlderExplicitToken()
    {
        var service = new CurrentUserPermissionService(new ExplicitPermissionCurrentUser(
            [ApplicationRoles.Developer], []));
        Assert.True(service.HasPermission(ApplicationPermissions.Quotations.ConfirmSent));
        Assert.False(service.HasPermission(ApplicationPermissions.Pricing.Manage));
    }

    [Fact]
    public void ExplicitPermissionSet_OverridesLegacyRoleMapping()
    {
        var currentUser = new ExplicitPermissionCurrentUser(
            roles: [ApplicationRoles.Sales.SaleUser],
            permissions: [ApplicationPermissions.Pricing.ViewSystemCalculatedPrice]);
        var service = new CurrentUserPermissionService(currentUser);

        Assert.True(service.HasPermission(ApplicationPermissions.Pricing.ViewSystemCalculatedPrice));
        Assert.False(service.HasPermission(ApplicationPermissions.Pricing.ViewApprovedSellingPrice));
    }

    [Fact]
    public void ExplicitEmptyPermissionSet_CanRevokeEveryLegacyPermission()
    {
        var currentUser = new ExplicitPermissionCurrentUser(
            roles: [ApplicationRoles.President],
            permissions: []);
        var service = new CurrentUserPermissionService(currentUser);

        Assert.False(service.HasPermission(ApplicationPermissions.Pricing.Manage));
        Assert.False(service.HasPermission(ApplicationPermissions.PLM.ViewFormulaPrices));
    }

    [Theory]
    [InlineData(ApplicationPermissions.PLM.ViewSampleRequestTracking)]
    [InlineData(ApplicationPermissions.PLM.ViewSaleOrderTracking)]
    [InlineData(ApplicationPermissions.PLM.ViewProductionTracking)]
    [InlineData(ApplicationPermissions.PLM.ViewSampleProductionTracking)]
    [InlineData(ApplicationPermissions.Warehouse.ViewStockTracking)]
    [InlineData(ApplicationPermissions.Dispatch.ViewDeliveryTracking)]
    [InlineData(ApplicationPermissions.Reports.ViewDeliveryOtd)]
    [InlineData(ApplicationPermissions.Reports.ViewDeliveryFinish)]
    [InlineData(ApplicationPermissions.CRM.ViewCustomerTracking)]
    [InlineData(ApplicationPermissions.Quotations.ViewTracking)]
    public void ImsTrackingClaimsRespectExplicitGrantsAndLegacyRoleMapping(string permission)
    {
        var ims = ApplicationRoles.InternalSystems.IMSUser;
        var developer = ApplicationRoles.Developer;
        var legacy = new CurrentUserPermissionService(new ExplicitPermissionCurrentUser([ims], [], false));
        var explicitGrant = new CurrentUserPermissionService(new ExplicitPermissionCurrentUser([ims, developer], [permission]));
        var explicitRevoked = new CurrentUserPermissionService(new ExplicitPermissionCurrentUser([ims, developer], []));
        var unrelatedRole = new CurrentUserPermissionService(new ExplicitPermissionCurrentUser([developer], [], false));

        Assert.Contains(permission, ApplicationPermissionCatalog.ResolveRole(ims, false, []));
        Assert.DoesNotContain(permission, ApplicationPermissionCatalog.ResolveRole(ims, true, []));
        Assert.True(legacy.HasPermission(permission));
        Assert.True(explicitGrant.HasPermission(permission));
        Assert.False(explicitRevoked.HasPermission(permission));
        Assert.False(unrelatedRole.HasPermission(permission));
    }

    private sealed class ExplicitPermissionCurrentUser(
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        bool explicitPermissions = true) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "database-permission-test";
        public string? Email => "database-permission-test@example.com";
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public IReadOnlyCollection<string> Permissions { get; } = permissions;
        public bool HasExplicitPermissionSet => explicitPermissions;

        public bool IsInRole(string role)
            => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
