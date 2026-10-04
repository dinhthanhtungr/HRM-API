using System.Text.Json;
using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Employees.Administration;
using HRM.Application.Features.Employees.Administration.GetEmployeeRolePermissions;
using HRM.Application.Features.Employees.Administration.SetEmployeeRolePermissions;
using HRM.Application.Features.Employees.Administration.SetEmployeeStatus;
using HRM.Application.Features.Employees.Administration.SetEmployeeAccountStatus;
using HRM.Application.Features.Employees.Dtos;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Identity;

namespace HRM.Application.Tests.Features.Employees;

public sealed class EmployeeAdministrationSecurityTests
{
    [Theory]
    [InlineData("Developer", true, true)]
    [InlineData("Admin", true, false)]
    [InlineData("President", true, false)]
    [InlineData("SaleUser", true, false)]
    [InlineData("Developer", false, false)]
    public async Task Only_authenticated_developer_can_read_or_write_global_role_permissions(
        string role, bool authenticated, bool allowed)
    {
        var user = new TestUser(role, authenticated);
        var service = new RoleService();
        var read = await new GetEmployeeRolePermissionsQueryHandler(user, service)
            .Handle(new(Guid.NewGuid()), default);
        var write = await new SetEmployeeRolePermissionsCommandHandler(user, service)
            .Handle(new() { RoleId = Guid.NewGuid(), Version = "v1", Permissions = [] }, default);
        Assert.Equal(allowed, read.Success);
        Assert.Equal(allowed, write.Success);
        Assert.Equal(allowed ? 2 : 0, service.Calls);
        if (!allowed)
        {
            Assert.Equal(EmployeeAdministrationError.Forbidden, read.Error);
            Assert.Equal(EmployeeAdministrationError.Forbidden, write.Error);
            Assert.Null(read.Data);
        }
    }

    [Fact]
    public async Task Unknown_missing_or_duplicate_permission_lists_never_reach_persistence()
    {
        var service = new RoleService();
        var handler = new SetEmployeeRolePermissionsCommandHandler(new TestUser("Developer"), service);
        IReadOnlyList<string>?[] lists = [null, ["unknown.permission"], ["pricing.manage", "pricing.manage"]];
        foreach (var permissions in lists)
        {
            var result = await handler.Handle(new() { Version = "v1", Permissions = permissions }, default);
            Assert.Equal(EmployeeAdministrationError.Validation, result.Error);
        }
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public void Company_scope_keeps_inactive_employees_but_excludes_other_companies()
    {
        var user = new TestUser("Admin");
        var own = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = user.CompanyId, IsActive = false };
        var foreign = new Employee { EmployeeId = Guid.NewGuid(), CompanyId = Guid.NewGuid() };
        var source = new[] { own, foreign }.AsQueryable();
        Assert.Equal([own], EmployeeAdministrationRules.ScopeEmployees(source, user).ToArray());
        Assert.Empty(EmployeeAdministrationRules.ScopeEmployees(source, new TestUser("Developer", false)));
        Assert.Equal(2, EmployeeAdministrationRules.ScopeEmployees(source, new TestUser("Developer")).Count());
        Assert.Empty(EmployeeAdministrationRules.ScopeEmployees(source, user with { CompanyId = null }));
    }

    [Fact]
    public void Lockout_blocks_existing_access_until_unlock_or_expiry()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(IdentityAccountAccessRules.IsLocked(true, DateTimeOffset.MaxValue, now));
        Assert.True(IdentityAccountAccessRules.IsLocked(true, now.AddMinutes(15), now));
        Assert.False(IdentityAccountAccessRules.IsLocked(true, now, now));
        Assert.False(IdentityAccountAccessRules.IsLocked(true, null, now));
        Assert.False(IdentityAccountAccessRules.IsLocked(false, DateTimeOffset.MaxValue, now));
    }

    [Fact]
    public void Empty_status_body_cannot_accidentally_lock_or_retire_employee()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SetEmployeeStatusCommand>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SetEmployeeAccountStatusCommand>("{}"));
    }

    [Fact]
    public void Explicit_empty_role_revokes_fallback_while_other_legacy_role_keeps_its_permissions()
    {
        Assert.Empty(ApplicationPermissionCatalog.ResolveRole("President", true, []));
        var legacy = ApplicationPermissionCatalog.ResolveRole("Developer", false, []);
        Assert.NotEmpty(legacy);
        var custom = ApplicationPermissionCatalog.ResolveRole("Custom", true, ["pricing.manage", "unknown"]);
        Assert.Equal(["pricing.manage"], custom);
        Assert.Contains("pricing.manage", legacy.Concat(custom).Distinct());
        Assert.Empty(ApplicationPermissionCatalog.ResolveRole("Custom", false, ["pricing.manage"]));
    }

    private sealed record TestUser(string Role, bool Authenticated = true) : ICurrentUser
    {
        public bool IsAuthenticated => Authenticated;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => "test-user";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [Role];
        public bool IsInRole(string role) => role == Role;
    }

    private sealed class RoleService : IEmployeeRolePermissionService
    {
        public int Calls { get; private set; }
        public Task<EmployeeRolePermissionsDto?> GetAsync(Guid roleId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<EmployeeRolePermissionsDto?>(new(roleId, "Custom", "v1", false, [], []));
        }
        public Task<EmployeeAdministrationResult<EmployeeRolePermissionsDto>> ReplaceAsync(
            Guid roleId, string version, IReadOnlyList<string> permissions, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(EmployeeAdministrationResult<EmployeeRolePermissionsDto>.Ok(
                new(roleId, "Custom", "v2", true, permissions, [])));
        }
    }
}
