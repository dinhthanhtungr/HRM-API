using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Pricing.Authorization;

namespace HRM.Application.Tests.Commons.Authorization;

public sealed class PricingVisibilityServiceTests
{
    [Theory]
    [InlineData(ApplicationRoles.Sales.SaleUser, true, false, false, false, false)]
    [InlineData(ApplicationRoles.President, true, true, true, true, true)]
    [InlineData(ApplicationRoles.Developer, true, true, true, true, true)]
    [InlineData(ApplicationRoles.Admin, true, true, true, true, false)]
    [InlineData(ApplicationRoles.Sales.PriceView, true, true, false, false, false)]
    [InlineData(ApplicationRoles.Accounting.ACCUser, true, true, true, false, false)]
    [InlineData(ApplicationRoles.Production.PLPUUser, false, false, true, false, false)]
    [InlineData(ApplicationRoles.Lab.LabUser, false, false, false, false, false)]
    [InlineData(ApplicationRoles.SeePrice.SeePriceUser, true, true, true, false, false)]
    [InlineData(ApplicationRoles.Leader, false, false, false, false, false)]
    public void GetAccess_MapsRolesToPricingCapabilities(
        string role,
        bool canViewApprovedPrice,
        bool canViewSystemPrice,
        bool canViewMaterialCost,
        bool canViewInternalPricing,
        bool canManage)
    {
        var permissionService = new CurrentUserPermissionService(new TestCurrentUser(role));
        var service = new PricingVisibilityService(permissionService);

        var access = service.GetAccess();

        Assert.Equal(
            role is ApplicationRoles.Sales.SaleUser or ApplicationRoles.President or ApplicationRoles.Developer,
            access.CanViewWorkbench);
        Assert.Equal(canViewApprovedPrice, access.CanViewApprovedSellingPrice);
        Assert.Equal(canViewSystemPrice, access.CanViewSystemCalculatedPrice);
        Assert.Equal(canViewMaterialCost, access.CanViewMaterialCost);
        Assert.Equal(canViewInternalPricing, access.CanViewManufacturingCost);
        Assert.Equal(canViewInternalPricing, access.CanViewMargin);
        Assert.Equal(canViewInternalPricing, access.CanViewHistory);
        Assert.Equal(canManage, access.CanManage);
        Assert.Equal(canManage, access.CanApprove);
    }

    [Fact]
    public void HasPermission_ReturnsFalseForUnknownPermission()
    {
        var service = new CurrentUserPermissionService(
            new TestCurrentUser(ApplicationRoles.Developer));

        Assert.False(service.HasPermission("pricing.unknown"));
    }

    [Fact]
    public void HasPermission_MatchesRoleCaseInsensitively()
    {
        var service = new CurrentUserPermissionService(
            new TestCurrentUser(ApplicationRoles.Sales.SaleUser.ToLowerInvariant()));

        Assert.True(service.HasPermission(
            ApplicationPermissions.Pricing.ViewApprovedSellingPrice));
    }

    [Fact]
    public void QuotationPricingOptions_DoesNotGrantFeatureAccessFromBroadPriceCapability()
    {
        var service = new PricingVisibilityService(
            new CurrentUserPermissionService(
                new TestCurrentUser(ApplicationRoles.Sales.PriceView)));

        var access = PricingAccessScopes.ForQuotationPricingOptions(service.GetAccess());

        Assert.False(access.CanViewWorkbench);
        Assert.False(access.CanViewApprovedSellingPrice);
        Assert.False(access.CanViewSystemCalculatedPrice);
        Assert.False(access.CanViewMaterialCost);
    }

    private sealed class TestCurrentUser(params string[] roles) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "authorization-test";
        public string? Email => "authorization-test@example.com";
        public IReadOnlyCollection<string> Roles { get; } = roles;

        public bool IsInRole(string role)
            => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
