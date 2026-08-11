using HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing;

namespace HRM.Application.Tests.Features.Reports;

public sealed class ExecutivePnLDeliveryCostRulesTests
{
    [Fact]
    public void ResolveAmount_UsesLotSnapshotWhenNormalizedLotsExist()
    {
        var result = ExecutivePnLDeliveryCostRules.ResolveAmount(true, 125m, 999m);

        Assert.Equal(125m, result);
    }

    [Fact]
    public void ResolveAmount_UsesLegacyAmountWhenNoNormalizedLotsExist()
    {
        var result = ExecutivePnLDeliveryCostRules.ResolveAmount(false, 125m, 999m);

        Assert.Equal(999m, result);
    }
}
