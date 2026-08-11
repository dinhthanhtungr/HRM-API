using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.BackfillLotConsumptions;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderLotBackfillReconciliationRulesTests
{
    [Fact]
    public void Build_ReportsConvertedSkippedAndQuantityDifference()
    {
        var convertedId = Guid.NewGuid();
        var mismatchedId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var source = new[]
        {
            new DeliveryOrderLotBackfillSource(convertedId, "LOT-A", 10m),
            new DeliveryOrderLotBackfillSource(mismatchedId, "LOT-B", 8m),
            new DeliveryOrderLotBackfillSource(missingId, "LOT-C", 5m),
            new DeliveryOrderLotBackfillSource(Guid.NewGuid(), null, 99m)
        };
        var consumption = new Dictionary<Guid, decimal>
        {
            [convertedId] = 10m,
            [mismatchedId] = 7m
        };

        var result = DeliveryOrderLotBackfillReconciliationRules.Build(source, consumption);

        Assert.Equal(3, result.LegacyRowCount);
        Assert.Equal(2, result.ConvertedRowCount);
        Assert.Equal(1, result.QuantityMismatchRowCount);
        Assert.Equal(23m, result.LegacyQuantityTotal);
        Assert.Equal(17m, result.ConsumptionQuantityTotal);
        Assert.Equal(-6m, result.QuantityDifference);
    }
}
