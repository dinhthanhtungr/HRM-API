using HRM.Application.Commons.Reporting.Costing;

namespace HRM.Application.Tests.Features.Reports;

public sealed class DeliveryCostRulesTests
{
    [Fact]
    public void ResolveAmount_UsesLotSnapshotWhenNormalizedLotsExist()
    {
        var result = DeliveryCostRules.ResolveAmount(true, 125m, 999m);

        Assert.Equal(125m, result);
    }

    [Fact]
    public void ResolveAmount_UsesLegacyAmountWhenNoNormalizedLotsExist()
    {
        var result = DeliveryCostRules.ResolveAmount(false, 125m, 999m);

        Assert.Equal(999m, result);
    }

    [Fact]
    public void ResolveAmount_UsesFallbackWhenNormalizedLotSnapshotIsZero()
    {
        var result = DeliveryCostRules.ResolveAmount(true, 0m, 999m);

        Assert.Equal(999m, result);
    }
}
