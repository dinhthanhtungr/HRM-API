using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Commands.ConfirmCustomerPrice;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestWorkflowAuthorizationTests
{
    [Theory]
    [InlineData(false, "SaleUser")]
    [InlineData(true, "LabUser")]
    [InlineData(true, "LabAdmin")]
    public async Task PriceConfirmationRejectsUnauthorizedUserBeforeAccessingStorage(bool authenticated, string role)
    {
        var user = new User(authenticated, role);
        var permissions = new CurrentUserPermissionService(user);
        var handler = new ConfirmCustomerPriceCommandHandler(null!, user, permissions, null!, null!, null!);
        var result = await handler.Handle(new ConfirmCustomerPriceCommand { SampleRequestId = Guid.NewGuid(), Reference = "Accepted quotation" }, CancellationToken.None);
        Assert.False(result.Success);
    }

    [Fact]
    public void SaleCanConfirmPriceButCannotReceiveStandards()
    {
        var permissions = new CurrentUserPermissionService(new User(true, ApplicationRoles.Sales.SaleUser));
        Assert.True(permissions.HasPermission(ApplicationPermissions.PLM.ConfirmCustomerPrice));
        Assert.False(permissions.HasPermission(ApplicationPermissions.PLM.ReceiveLabStandard));
    }

    private sealed class User(bool authenticated, string role) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "workflow-test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string candidate) => candidate == role;
    }
}
