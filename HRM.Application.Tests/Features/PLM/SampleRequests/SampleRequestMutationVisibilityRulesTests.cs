using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestMutationVisibilityRulesTests
{
    [Fact]
    public void Resolve_ForSaleUser_EnablesOnlyInternalCustomerVisibility()
    {
        var scope = CreateScope(canViewInternalCustomer: false);

        var result = SampleRequestMutationVisibilityRules.Resolve(scope, isSaleUser: true);

        Assert.True(result.CanViewInternalCustomer);
        Assert.False(result.HasFullCustomerView);
        Assert.Equal(scope.CompanyId, result.CompanyId);
        Assert.Equal(scope.EmployeeIdsInScope, result.EmployeeIdsInScope);
    }

    [Fact]
    public void Resolve_ForNonSaleUser_PreservesScope()
    {
        var scope = CreateScope(canViewInternalCustomer: false);

        var result = SampleRequestMutationVisibilityRules.Resolve(scope, isSaleUser: false);

        Assert.Same(scope, result);
    }

    private static ViewerScope CreateScope(bool canViewInternalCustomer)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            HasFullCustomerView: false,
            CanViewInternalCustomer: canViewInternalCustomer,
            LeaderGroupIds: new HashSet<Guid> { Guid.NewGuid() },
            EmployeeIdsInScope: new HashSet<Guid> { Guid.NewGuid() },
            Now: DateTime.Now);
}
