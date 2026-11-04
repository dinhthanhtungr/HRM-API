using System.Reflection;
using HRM.Application.Abstractions.Identity;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Employees.Administration;
using HRM.Application.Features.Employees.Administration.GetEmployeeRoleAccounts;
using HRM.Application.Features.Employees.Dtos;

namespace HRM.Application.Tests.Features.Employees;

public sealed class EmployeeRoleAccountsTests
{
    [Theory]
    [InlineData("Developer", true, true)]
    [InlineData("Admin", true, false)]
    [InlineData("President", true, true)]
    [InlineData("HC_HRUser", true, true)]
    [InlineData("HCHRUser", true, true)]
    [InlineData("SaleUser", true, false)]
    [InlineData("Developer", false, false)]
    public async Task Accounts_are_only_queried_for_authorized_roles_in_their_scope(
        string role, bool authenticated, bool allowed)
    {
        var service = DispatchProxy.Create<IEmployeeIdentityAdministrationService, AccountService>();
        var proxy = (AccountService)service;
        var result = await new GetEmployeeRoleAccountsQueryHandler(new User(role, authenticated), service)
            .Handle(new() { RoleId = Guid.NewGuid() }, default);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed ? 1 : 0, proxy.Calls);
        if (allowed) Assert.Equal(role == "Developer" ? null : User.Company, proxy.CompanyId);
        if (!allowed)
        {
            Assert.Equal(EmployeeAdministrationError.Forbidden, result.Error);
            Assert.Null(result.Data);
        }
    }

    [Fact]
    public async Task Missing_role_returns_not_found_and_invalid_inputs_do_not_query()
    {
        var service = DispatchProxy.Create<IEmployeeIdentityAdministrationService, AccountService>();
        var proxy = (AccountService)service;
        proxy.Missing = true;
        var handler = new GetEmployeeRoleAccountsQueryHandler(new User("Developer", true), service);
        var missing = await handler.Handle(new() { RoleId = Guid.NewGuid() }, default);
        Assert.Equal(EmployeeAdministrationError.NotFound, missing.Error);
        var invalid = await handler.Handle(new() { RoleId = Guid.Empty }, default);
        Assert.Equal(EmployeeAdministrationError.Validation, invalid.Error);
        invalid = await handler.Handle(new() { RoleId = Guid.NewGuid(), Keyword = new string('x', 201) }, default);
        Assert.Equal(EmployeeAdministrationError.Validation, invalid.Error);
        Assert.Equal(1, proxy.Calls);
    }

    private sealed record User(string Role, bool IsAuthenticated) : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public static readonly Guid Company = Guid.NewGuid();
        public Guid? CompanyId { get; init; } = Company;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [Role];
        public bool IsInRole(string role) => role == Role;
    }

    public class AccountService : DispatchProxy
    {
        public int Calls { get; private set; }
        public bool Missing { get; set; }
        public Guid? CompanyId { get; private set; }
        public IReadOnlyList<EmployeeIdentityRole> Roles { get; set; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IEmployeeIdentityAdministrationService.GetRolesAsync))
                return Task.FromResult(Roles);
            Assert.Equal(nameof(IEmployeeIdentityAdministrationService.GetRoleAccountsAsync), targetMethod!.Name);
            Calls++;
            CompanyId = (Guid?)args![5];
            return Task.FromResult<PagedResult<EmployeeRoleAccountDto>?>(Missing
                ? null : new([], 0, (int)args![1]!, (int)args[2]!));
        }
    }

    [Fact]
    public async Task President_cannot_bypass_hidden_privileged_role_by_id()
    {
        var service = DispatchProxy.Create<IEmployeeIdentityAdministrationService, AccountService>();
        var proxy = (AccountService)service;
        var roleId = Guid.NewGuid();
        proxy.Roles = [new(roleId, "Developer", 1)];
        var result = await new GetEmployeeRoleAccountsQueryHandler(new User("President", true), service)
            .Handle(new() { RoleId = roleId }, default);
        Assert.Equal(EmployeeAdministrationError.Forbidden, result.Error);
        Assert.Equal(0, proxy.Calls);
    }

    [Fact]
    public async Task President_without_company_cannot_query_global_accounts()
    {
        var service = DispatchProxy.Create<IEmployeeIdentityAdministrationService, AccountService>();
        var result = await new GetEmployeeRoleAccountsQueryHandler(
            new User("President", true) { CompanyId = null }, service)
            .Handle(new() { RoleId = Guid.NewGuid() }, default);
        Assert.Equal(EmployeeAdministrationError.Forbidden, result.Error);
        Assert.Equal(0, ((AccountService)service).Calls);
    }
}
