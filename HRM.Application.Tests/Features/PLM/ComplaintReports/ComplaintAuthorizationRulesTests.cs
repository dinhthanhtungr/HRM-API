using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.ComplaintReports.Services;

namespace HRM.Application.Tests.Features.PLM.ComplaintReports;

public sealed class ComplaintAuthorizationRulesTests
{
    [Fact]
    public void SaleUser_CanCreateButCannotInvestigateOrApprove()
    {
        var user = User(ApplicationRoles.Sales.SaleUser);

        Assert.True(ComplaintAuthorizationRules.CanCreate(user));
        Assert.False(ComplaintAuthorizationRules.CanInvestigate(user));
        Assert.False(ComplaintAuthorizationRules.CanFinalApprove(user));
    }

    [Fact]
    public void QcUser_CanInvestigateAndVerifyButCannotFinalApprove()
    {
        var user = User(ApplicationRoles.Quality.QCUser);

        Assert.True(ComplaintAuthorizationRules.CanInvestigate(user));
        Assert.True(ComplaintAuthorizationRules.CanVerify(user));
        Assert.False(ComplaintAuthorizationRules.CanFinalApprove(user));
    }

    [Fact]
    public void Leader_CanApproveVerifyAndManageActions()
    {
        var user = User(ApplicationRoles.Leader);

        Assert.True(ComplaintAuthorizationRules.CanInitialApprove(user));
        Assert.True(ComplaintAuthorizationRules.CanFinalApprove(user));
        Assert.True(ComplaintAuthorizationRules.CanVerify(user));
        Assert.True(ComplaintAuthorizationRules.CanManageActions(user));
    }

    [Fact]
    public void AssignedEmployee_CanUpdateOwnActionWithoutManagementRole()
    {
        var employeeId = Guid.NewGuid();
        var user = new FakeCurrentUser(employeeId, Array.Empty<string>());

        Assert.True(ComplaintAuthorizationRules.CanUpdateAction(user, employeeId));
        Assert.False(ComplaintAuthorizationRules.CanUpdateAction(user, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(ApplicationRoles.Sales.SaleUser)]
    [InlineData(ApplicationRoles.Quality.QCUser)]
    [InlineData(ApplicationRoles.Leader)]
    [InlineData(ApplicationRoles.Admin)]
    public void ComplaintPdfViewer_CanExportPdf(string role)
        => Assert.True(ComplaintAuthorizationRules.CanViewPdf(User(role)));

    [Fact]
    public void UserOutsideComplaintPdfRoles_CannotExportPdf()
        => Assert.False(ComplaintAuthorizationRules.CanViewPdf(User("UnrelatedRole")));

    private static ICurrentUser User(params string[] roles)
        => new FakeCurrentUser(Guid.NewGuid(), roles);

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(Guid employeeId, IReadOnlyCollection<string> roles)
        {
            EmployeeId = employeeId;
            Roles = roles;
        }

        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; }
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => "test@example.com";
        public IReadOnlyCollection<string> Roles { get; }
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
