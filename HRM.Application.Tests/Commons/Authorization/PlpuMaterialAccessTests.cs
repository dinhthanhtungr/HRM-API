using HRM.Application.Commons.Authorization;
using HRM.Application.Tests.Features.DevAndQA.IQC;

namespace HRM.Application.Tests.Commons.Authorization;

public sealed class PlpuMaterialAccessTests
{
    public static TheoryData<string> MaterialPermissions => new()
    {
        ApplicationPermissions.Purchasing.ManageCatalog,
        ApplicationPermissions.Purchasing.ManagePurchaseOrders,
        ApplicationPermissions.Pricing.ViewMaterialCost,
        ApplicationPermissions.PLM.ViewFormulaMaterials,
        ApplicationPermissions.PLM.ViewMaterialPriceReviewDetails,
        ApplicationPermissions.PLM.ExtractMaterialDocuments
    };

    [Theory]
    [MemberData(nameof(MaterialPermissions))]
    public void PlpuHasMaterialPermission_InLegacyAndSeededExplicitTokens(string permission)
    {
        foreach (var explicitSet in new[] { false, true })
        {
            var user = new IqcWorkflowTests.User { Roles = [ApplicationRoles.Production.PLPUUser],
                HasExplicitPermissionSet = explicitSet, Permissions = explicitSet ? [permission] : [] };
            Assert.True(new CurrentUserPermissionService(user).HasPermission(permission));
            Assert.Contains(permission, ApplicationPermissionCatalog.ResolveRole(ApplicationRoles.Production.PLPUUser,
                explicitSet, [permission]));
            var anonymous = new IqcWorkflowTests.User { IsAuthenticated = false,
                Roles = user.Roles, Permissions = user.Permissions, HasExplicitPermissionSet = explicitSet };
            Assert.False(new CurrentUserPermissionService(anonymous).HasPermission(permission));
        }
    }

    [Theory]
    [MemberData(nameof(MaterialPermissions))]
    public void SaleAndUnknownPermissionsRemainDenied(string permission)
    {
        var service = new CurrentUserPermissionService(new IqcWorkflowTests.User { Roles = [ApplicationRoles.Sales.SaleUser] });
        Assert.False(service.HasPermission(permission));
        Assert.False(service.HasPermission("unknown.permission"));
    }

    [Fact]
    public void MaterialRolePoliciesAllowPlpu_WithoutGrantingSuperUserOrFormulaPrices()
    {
        var role = ApplicationRoles.Production.PLPUUser;
        Assert.Contains(role, ApplicationRoleSets.PLM.MaterialSupplierPriceEditors);
        Assert.Contains(role, ApplicationRoleSets.PLM.MaterialPurchaseAvailabilityManagers);
        Assert.Contains(role, ApplicationRoleSets.PLM.MaterialReplacementManagers);
        Assert.Contains(role, ApplicationRoleSets.PLM.MaterialReplacementViewers);
        Assert.Contains(role, ApplicationRoleSets.PLM.MaterialPriceReviewViewers);
        Assert.DoesNotContain(role, ApplicationRoleSets.SuperUsers);
        var service = new CurrentUserPermissionService(new IqcWorkflowTests.User { Roles = [role] });
        Assert.False(service.HasPermission(ApplicationPermissions.Pricing.Manage));
        Assert.False(service.HasPermission(ApplicationPermissions.PLM.ViewFormulaPrices));
        // Database permission sets remain authoritative; role fallback must not override a revoked catalog claim.
        Assert.False(new CurrentUserPermissionService(new IqcWorkflowTests.User { Roles = [role], HasExplicitPermissionSet = true })
            .HasPermission(ApplicationPermissions.Purchasing.ManageCatalog));
    }
}
