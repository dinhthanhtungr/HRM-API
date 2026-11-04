using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SaleOrders.Services;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class StandardSampleConfirmationRulesTests
{
    [Theory]
    [InlineData(ApplicationRoles.Developer)]
    [InlineData(ApplicationRoles.President)]
    [InlineData(ApplicationRoles.Leader)]
    [InlineData(ApplicationRoles.Sales.SaleAdmin)]
    public void NamedRoles_CanConfirmOtherSalesOrdersWithinCompany(string role)
    {
        var user = new User(role);
        Assert.True(StandardSampleConfirmationRules.CanConfirm(user, user.CompanyId!.Value, Guid.NewGuid()));
        Assert.True(StandardSampleConfirmationRules.CanConfirmOtherOwners(user));
        Assert.False(StandardSampleConfirmationRules.CanConfirm(user, Guid.NewGuid(), user.EmployeeId!.Value));
    }

    [Theory]
    [InlineData(ApplicationRoles.Sales.SaleUser)]
    [InlineData(ApplicationRoles.Admin)]
    [InlineData(ApplicationRoles.Accounting.ACCUser)]
    [InlineData(ApplicationRoles.Lab.LabUser)]
    public void OtherRoles_RequireOwnership(string role)
    {
        var user = new User(role);
        Assert.False(StandardSampleConfirmationRules.CanConfirm(user, user.CompanyId!.Value, Guid.NewGuid()));
        Assert.False(StandardSampleConfirmationRules.CanConfirmOtherOwners(user));
        Assert.True(StandardSampleConfirmationRules.CanConfirm(user, user.CompanyId.Value, user.EmployeeId!.Value));
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("employee")]
    [InlineData("company")]
    public void MissingIdentityContext_FailsClosed(string missing)
    {
        var user = new User(ApplicationRoles.Developer);
        var company = user.CompanyId!.Value;
        var owner = user.EmployeeId!.Value;
        if (missing == "anonymous") user.IsAuthenticated = false;
        if (missing == "employee") user.EmployeeId = null;
        if (missing == "company") user.CompanyId = null;
        Assert.False(StandardSampleConfirmationRules.CanConfirm(user, company, owner));
        Assert.False(StandardSampleConfirmationRules.CanConfirmOtherOwners(user));
    }

    private sealed class User(params string[] roles) : ICurrentUser
    {
        public bool IsAuthenticated { get; set; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; set; } = Guid.NewGuid();
        public Guid? CompanyId { get; set; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
