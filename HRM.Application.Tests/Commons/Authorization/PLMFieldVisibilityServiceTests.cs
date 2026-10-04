using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Shared.Authorization;

namespace HRM.Application.Tests.Commons.Authorization;

public sealed class PLMFieldVisibilityServiceTests
{
    [Theory]
    [InlineData(ApplicationRoles.Sales.SaleUser, false, false, false)]
    [InlineData(ApplicationRoles.Sales.PriceView, true, false, false)]
    [InlineData(ApplicationRoles.Accounting.ACCUser, true, true, false)]
    [InlineData(ApplicationRoles.Production.PLPUUser, false, true, true)]
    [InlineData(ApplicationRoles.Lab.LabUser, false, true, true)]
    [InlineData(ApplicationRoles.SeePrice.SeePriceUser, true, true, false)]
    [InlineData(ApplicationRoles.President, true, true, true)]
    public void Capabilities_PreserveExistingPlmRoleMatrix(
        string role,
        bool canViewPrices,
        bool canViewMaterials,
        bool canViewTechnicalInfo)
    {
        var permissions = new CurrentUserPermissionService(new TestCurrentUser(true, role));
        var service = new PLMFieldVisibilityService(permissions);

        Assert.Equal(canViewPrices, service.CanViewFormulaPrices());
        Assert.Equal(canViewMaterials, service.CanViewFormulaMaterials());
        Assert.Equal(canViewTechnicalInfo, service.CanViewProductTechnicalInfo());
    }

    [Fact]
    public void UnauthenticatedUser_HasNoSensitiveCapabilitiesEvenWithRoleClaim()
    {
        var permissions = new CurrentUserPermissionService(
            new TestCurrentUser(false, ApplicationRoles.President));
        var service = new PLMFieldVisibilityService(permissions);

        Assert.False(service.CanViewFormulaPrices());
        Assert.False(service.CanViewFormulaMaterials());
        Assert.False(service.CanViewProductTechnicalInfo());
    }

    private sealed class TestCurrentUser(bool isAuthenticated, params string[] roles) : ICurrentUser
    {
        public bool IsAuthenticated { get; } = isAuthenticated;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "plm-visibility-test";
        public string? Email => "plm-visibility-test@example.com";
        public IReadOnlyCollection<string> Roles { get; } = roles;

        public bool IsInRole(string role)
            => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
