using HRM.Application.Commons.Deliveries;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderLotReadRulesTests
{
    [Fact]
    public void ResolveDisplay_NormalizedLotsOverrideLegacyValue()
    {
        var result = DeliveryOrderLotReadRules.ResolveDisplay(
            ["LOT-B", "LOT-A", "lot-a"],
            "LEGACY-X");

        Assert.Equal("LOT-A, LOT-B", result);
    }

    [Fact]
    public void ResolveDisplay_UsesLegacyOnlyWhenNormalizedLotsAreMissing()
    {
        var result = DeliveryOrderLotReadRules.ResolveDisplay([], "  LOT-OLD  ");

        Assert.Equal("LOT-OLD", result);
    }

    [Fact]
    public void SplitLegacy_NormalizesSeparatorsAndDuplicates()
    {
        var result = DeliveryOrderLotReadRules.SplitLegacy("LOT-A; lot-a | LOT-B");

        Assert.Equal(["LOT-A", "LOT-B"], result);
    }
}
