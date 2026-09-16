using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Tests.Commons.Authorization;

public sealed class DatabaseBackedPermissionResolutionTests
{
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

    private sealed class ExplicitPermissionCurrentUser(
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "database-permission-test";
        public string? Email => "database-permission-test@example.com";
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public IReadOnlyCollection<string> Permissions { get; } = permissions;
        public bool HasExplicitPermissionSet => true;

        public bool IsInRole(string role)
            => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
