using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateInternalProductionOrder;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateProductionOrderInform;

namespace HRM.Application.Tests.Features.PLM.ProductionOrders;

public sealed class ProductionOrderAuthorizationTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Developer", true)]
    [InlineData("President", true)]
    [InlineData("PLPUUser", true)]
    [InlineData("QLSXUser", true)]
    [InlineData("ManufactureUser", true)]
    [InlineData("SaleUser", false)]
    [InlineData("LabUser", false)]
    [InlineData("KHOUser", false)]
    public void LegacyRoleMatrix(string role, bool expected)
    {
        var user = new ProductionOrderCreationTests.TestUser { Roles = [role] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.Equal(expected, permissions.HasPermission(ApplicationPermissions.PLM.CreateProductionOrders));
        Assert.False(permissions.HasPermission("plm.production-order.unknown"));
    }

    [Theory]
    [InlineData("sale")]
    [InlineData("anonymous")]
    [InlineData("revoked")]
    [InlineData("no-company")]
    [InlineData("no-employee")]
    public async Task Handlers_FailClosedBeforeAnyDbAccess(string scenario)
    {
        var user = new ProductionOrderCreationTests.TestUser();
        if (scenario == "sale") user.Roles = [ApplicationRoles.Sales.SaleUser];
        if (scenario == "anonymous") user.IsAuthenticated = false;
        if (scenario == "revoked") user.HasExplicitPermissionSet = true;
        if (scenario == "no-company") user.CompanyId = null;
        if (scenario == "no-employee") user.EmployeeId = null;
        var permissions = new CurrentUserPermissionService(user);
        var internalHandler = new CreateInternalProductionOrderCommandHandler(
            null!, user, permissions, null!, null!, null!);
        var informHandler = new CreateProductionOrderInformCommandHandler(
            null!, user, permissions, null!, null!, null!, null!, null!);
        Assert.False((await internalHandler.Handle(new(new()), default)).Success);
        Assert.False((await informHandler.Handle(new(new()), default)).Success);
    }

    [Fact]
    public void ExplicitPermissionSet_CanGrantOrRevokeIndependentOfRole()
    {
        var user = new ProductionOrderCreationTests.TestUser
        {
            Roles = [ApplicationRoles.Sales.SaleUser], HasExplicitPermissionSet = true,
            Permissions = [ApplicationPermissions.PLM.CreateProductionOrders]
        };
        var permissions = new CurrentUserPermissionService(user);
        Assert.True(permissions.HasPermission(ApplicationPermissions.PLM.CreateProductionOrders));
        user.Permissions = [];
        user.Roles = [ApplicationRoles.President];
        Assert.False(permissions.HasPermission(ApplicationPermissions.PLM.CreateProductionOrders));
        Assert.Contains(ApplicationPermissions.PLM.CreateProductionOrders, ApplicationPermissionCatalog.Codes);
    }
}
