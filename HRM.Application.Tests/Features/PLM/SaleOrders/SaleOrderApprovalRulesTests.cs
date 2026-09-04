using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SaleOrders.Services;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class SaleOrderApprovalRulesTests
{
    [Theory]
    [InlineData(ApplicationRoles.Admin)]
    [InlineData(ApplicationRoles.Developer)]
    [InlineData(ApplicationRoles.President)]
    [InlineData(ApplicationRoles.Leader)]
    [InlineData(ApplicationRoles.Sales.SaleUser)]
    public void CanAutoApproveOnCreate_AllowsApproversAndRegularSale(string role)
        => Assert.True(SaleOrderApprovalRules.CanAutoApproveOnCreate(new CurrentUser(role)));

    [Theory]
    [InlineData(ApplicationRoles.Accounting.ACUser)]
    [InlineData(ApplicationRoles.Accounting.HNUser)]
    public void CanAutoApproveOnCreate_RejectsAccountingRolesWithoutAnApproverRole(string role)
        => Assert.False(SaleOrderApprovalRules.CanAutoApproveOnCreate(
            new CurrentUser(ApplicationRoles.Sales.SaleUser, role)));

    private sealed class CurrentUser(params string[] roles) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => "test@example.com";
        public IReadOnlyCollection<string> Roles { get; } = roles;

        public bool IsInRole(string targetRole)
            => Roles.Contains(targetRole, StringComparer.OrdinalIgnoreCase);
    }
}
