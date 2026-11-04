using HRM.Application.Commons.Authorization;
using HRM.Application.Tests.Features.DevAndQA.IQC;

namespace HRM.Application.Tests.Commons.Authorization;

public sealed class PlpuPurchasingAccessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlpuRoleGrantsPurchasingWithoutPermissionClaim(bool explicitSet)
    {
        var user = new IqcWorkflowTests.User { Roles = [ApplicationRoles.Production.PLPUUser], HasExplicitPermissionSet = explicitSet };
        var permissions = new CurrentUserPermissionService(user);
        Assert.True(permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders));
        Assert.Contains(ApplicationRoles.Production.PLPUUser, ApplicationRoleSets.Modules.Purchasing);
        Assert.False(permissions.HasPermission(ApplicationPermissions.Pricing.Manage));
        Assert.False(permissions.HasPermission("unknown.permission"));
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("QCUser")]
    public void NearbyRolesDoNotGainPurchasing(string role)
    {
        Assert.False(new CurrentUserPermissionService(new IqcWorkflowTests.User { Roles = [role] })
            .HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders));
    }

    [Fact]
    public void AnonymousPlpuIsDenied()
    {
        Assert.False(new CurrentUserPermissionService(new IqcWorkflowTests.User
        {
            IsAuthenticated = false, Roles = [ApplicationRoles.Production.PLPUUser], HasExplicitPermissionSet = true
        }).HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders));
    }

    [Fact]
    public void ExplicitEmptyPurchaserPermissionsRemainRevoked()
    {
        Assert.False(new CurrentUserPermissionService(new IqcWorkflowTests.User
        {
            Roles = [ApplicationRoles.Purchasing.Purchaser], HasExplicitPermissionSet = true
        }).HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders));
    }

    [Fact]
    public async Task PlpuRoleDoesNotOpenForeignCompanyPurchaseOrders()
    {
        var (db, store) = IqcTestStore.CreateContext<HRM.Application.Abstractions.Persistence.Purchasing.IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = [ApplicationRoles.Production.PLPUUser], HasExplicitPermissionSet = true };
        var po = new HRM.Domain.Entities.OrderSchema.PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true
        };
        store.Seed(nameof(db.PurchaseOrders), po);
        var handler = new HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById.GetPurchaseOrderByIdQueryHandler(db, user, null!);
        Assert.Null(await handler.Handle(new(po.PurchaseOrderId), default));
    }
}
