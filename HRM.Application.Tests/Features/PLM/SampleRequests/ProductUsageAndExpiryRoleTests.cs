using HRM.Application.Commons.Authorization;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class ProductUsageAndExpiryRoleTests
{
    [Theory]
    [InlineData(ApplicationRoles.Developer, true)]
    [InlineData(ApplicationRoles.President, true)]
    [InlineData(ApplicationRoles.Lab.LabUser, true)]
    [InlineData(ApplicationRoles.Production.PLPUUser, false)]
    [InlineData(ApplicationRoles.Sales.SaleUser, false)]
    [InlineData(ApplicationRoles.Admin, false)]
    [InlineData("", false)]
    public void OnlyExplicitEditorsCanChangeUsageAndExpiry(string role, bool allowed)
        => Assert.Equal(allowed, ApplicationRoleSets.PLM.ProductUsageAndExpiryEditors.Contains(role));
}
