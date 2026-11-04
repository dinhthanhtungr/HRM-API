using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Dispatch.DeliveryOrders;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderAccessRulesTests
{
    [Theory]
    [InlineData("DispatchUser")]
    [InlineData("PLPUUser")]
    public void DispatchManager_CanReadManageAndCancel(string role)
    {
        var user = CreateUser(role);

        Assert.True(DeliveryOrderAccessRules.CanRead(user));
        Assert.True(DeliveryOrderAccessRules.CanManage(user));
        Assert.True(DeliveryOrderAccessRules.CanCancel(user));
    }

    [Theory]
    [InlineData("User")]
    [InlineData("PLPUNotify")]
    [InlineData("ManufactureUser")]
    public void UnrelatedRole_HasNoDeliveryPermission(string role)
    {
        var user = CreateUser(role);
        Assert.False(DeliveryOrderAccessRules.CanRead(user));
        Assert.False(DeliveryOrderAccessRules.CanManage(user));
        Assert.False(DeliveryOrderAccessRules.CanCancel(user));
    }

    [Theory]
    [InlineData(DeliveryOrderAccessRules.ReadRoles)]
    [InlineData(DeliveryOrderAccessRules.ManageRoles)]
    [InlineData(DeliveryOrderAccessRules.CancelRoles)]
    public void ControllerRoleSets_IncludeBothDispatchManagers(string roles)
    {
        Assert.Contains("PLPUUser", roles.Split(','));
        Assert.Contains("DispatchUser", roles.Split(','));
    }

    [Fact]
    public void SaleUser_CanReadButCannotManageLifecycle()
    {
        var user = CreateUser("SaleUser");

        Assert.True(DeliveryOrderAccessRules.CanRead(user));
        Assert.False(DeliveryOrderAccessRules.CanManage(user));
        Assert.False(DeliveryOrderAccessRules.CanCancel(user));
    }

    [Fact]
    public void AccountingUser_CanReadButACUserCannot()
    {
        Assert.True(DeliveryOrderAccessRules.CanRead(CreateUser("ACCUser")));
        Assert.False(DeliveryOrderAccessRules.CanRead(CreateUser("ACUser")));
    }

    [Fact]
    public void UnauthenticatedUser_HasNoDeliveryOrderPermission()
    {
        var user = new TestCurrentUser
        {
            IsAuthenticated = false,
            Roles = ["Admin"]
        };

        Assert.False(DeliveryOrderAccessRules.CanRead(user));
        Assert.False(DeliveryOrderAccessRules.CanManage(user));
        Assert.False(DeliveryOrderAccessRules.CanCancel(user));
    }

    private static TestCurrentUser CreateUser(params string[] roles)
        => new()
        {
            IsAuthenticated = true,
            CompanyId = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            Roles = roles
        };

    private sealed class TestCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated { get; init; }
        public Guid UserId { get; init; } = Guid.NewGuid();
        public Guid? EmployeeId { get; init; }
        public Guid? CompanyId { get; init; }
        public string? UserName { get; init; }
        public string? Email { get; init; }
        public IReadOnlyCollection<string> Roles { get; init; } = [];

        public bool IsInRole(string role)
            => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
