using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using HRM.Application.Features.Employees.Administration;
using HRM.Application.Features.Warehouse.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Tests.Commons.Authorization;

public sealed class AccountingReadAccessTests
{
    [Fact]
    public void Accounting_HasEveryPresidentReadCapabilityInCompatibilityMapping()
    {
        var permissions = new CurrentUserPermissionService(new User());
        var presidentReadPermissions = ApplicationPermissionCatalog.Codes.Where(code =>
            code.EndsWith(".view", StringComparison.Ordinal) &&
            ApplicationPermissionRoleSets.GetRoles(code).Contains(ApplicationRoles.President));

        Assert.NotEmpty(presidentReadPermissions);
        Assert.All(presidentReadPermissions, code => Assert.True(permissions.HasPermission(code), code));
        Assert.False(permissions.HasPermission("unknown.resource.view"));
    }

    [Fact]
    public void Accounting_ReadPermissionDoesNotGrantTechnicalOrOperationalMutations()
    {
        var user = new User();
        var permissions = new CurrentUserPermissionService(user);
        Assert.False(permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageIqc));
        Assert.False(permissions.HasPermission(ApplicationPermissions.Equipment.Update));
        Assert.False(permissions.HasPermission(ApplicationPermissions.Equipment.Delete));
        Assert.False(permissions.HasPermission(ApplicationPermissions.PLM.CreateProductionOrders));
        Assert.False(permissions.HasPermission(ApplicationPermissions.PLM.CheckProductionOrders));
        Assert.False(permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders));
        Assert.DoesNotContain(ApplicationRoles.Accounting.ACCUser, ApplicationRoleSets.PLM.ProductTechnicalEditors);
        Assert.DoesNotContain(ApplicationRoles.Accounting.ACCUser, ApplicationRoleSets.CRM.CustomerEditors);
        Assert.DoesNotContain(ApplicationRoles.Accounting.ACCUser, ApplicationRoleSets.Notifications.ConversationParticipantManagers);
        Assert.True(ProductPricingAccessRules.CanViewManagementData(user));
        Assert.False(ProductPricingAccessRules.CanManage(user));
        Assert.True(ProductPricingReviewRules.GetReadContext(user).Success);
        Assert.False(ProductPricingReviewRules.GetContext(user).Success);
        Assert.True(EmployeeAdministrationRules.CanViewEmployees(user));
        Assert.False(EmployeeAdministrationRules.CanManageEmployees(user));
        Assert.False(EmployeeAdministrationRules.CanManageAllCompanies(user));
        Assert.False(EmployeeAdministrationRules.CanManageRoleTypes(user));
    }

    [Theory]
    [InlineData(ApplicationRoles.Accounting.ACCUser, true, false)]
    [InlineData(ApplicationRoles.President, true, true)]
    [InlineData(ApplicationRoles.Sales.SaleUser, false, false)]
    public async Task CustomerScope_SeparatesReadAccessFromMutationScope(
        string role, bool expectedRead, bool expectedMutation)
    {
        var user = new User { Roles = [role] };
        var service = CreateVisibilityService(user);
        Assert.Equal(expectedRead, (await service.BuildReadScopeAsync()).HasFullCustomerView);
        Assert.Equal(expectedMutation, (await service.BuildScopeAsync()).HasFullCustomerView);
    }

    [Fact]
    public async Task Accounting_ReadScopeStillExcludesOtherCompaniesAndInactiveCustomers()
    {
        var user = new User();
        var service = CreateVisibilityService(user);
        var scope = await service.BuildReadScopeAsync();
        var visible = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true };
        var otherCompany = new Customer { CustomerId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        var inactive = new Customer { CustomerId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = false };
        var result = service.ApplyCustomerVisibility(new[] { visible, otherCompany, inactive }.AsQueryable(), scope).ToList();
        Assert.Same(visible, Assert.Single(result));
    }

    [Fact]
    public async Task UnauthenticatedAccounting_CannotResolveReadScopeOrSensitivePermissions()
    {
        var user = new User { IsAuthenticated = false };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateVisibilityService(user).BuildReadScopeAsync());
        Assert.False(new CurrentUserPermissionService(user).HasPermission(ApplicationPermissions.PLM.ViewProductTechnicalInfo));
        Assert.False(ProductPricingAccessRules.CanViewManagementData(user));
        Assert.False(WarehouseVoucherAccessRules.CanRead(user));
    }

    [Fact]
    public void ExplicitEmptyDatabasePermissions_AreNotOverriddenByAccountingRole()
    {
        var user = new User { HasExplicitPermissionSet = true };
        var permissions = new CurrentUserPermissionService(user);
        Assert.False(permissions.HasPermission(ApplicationPermissions.PLM.ViewProductTechnicalInfo));
        Assert.False(permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewIqc));
        Assert.False(ProductPricingAccessRules.CanViewManagementData(user));
        Assert.False(ProductPricingReviewRules.GetReadContext(user).Success);
    }

    [Fact]
    public void Accounting_WarehouseReadRequiresCompanyContext()
    {
        Assert.True(WarehouseVoucherAccessRules.CanRead(new User()));
        Assert.True(WarehouseVoucherAccessRules.CanReadAll(new User()));
        Assert.False(WarehouseVoucherAccessRules.CanRead(new User { CompanyId = null }));
        Assert.False(WarehouseVoucherAccessRules.CanRead(new User { CompanyId = Guid.Empty }));
    }

    private static CustomerVisibilityService CreateVisibilityService(User user)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<ICustomerVisibilityReadDbContext>();
        store.Seed<MemberInGroup>(nameof(ICustomerVisibilityReadDbContext.MemberInGroups));
        return new CustomerVisibilityService(db, user, new Clock());
    }

    private sealed class Clock : IDateTimeProvider
    {
        public DateTime Now => new(2026, 10, 7);
    }

    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [ApplicationRoles.Accounting.ACCUser];
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
