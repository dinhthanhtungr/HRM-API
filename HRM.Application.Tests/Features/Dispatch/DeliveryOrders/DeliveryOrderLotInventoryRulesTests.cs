using HRM.Application.Features.Dispatch.DeliveryOrders;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderLotInventoryRulesTests
{
    [Fact]
    public void Validate_RejectsInsufficientLotStock()
    {
        var productId = Guid.NewGuid();
        var requested = new[] { new DeliveryOrderRequestedLot(productId, "FG-01", "LOT-A", 11m) };
        var stock = Snapshot(productId, "FG-01", "LOT-A", available: 10m, productAvailable: 20m);

        var error = DeliveryOrderLotInventoryRules.Validate(
            requested,
            new Dictionary<string, DeliveryOrderLotInventorySnapshot>
            {
                [DeliveryOrderLotInventoryRules.Key("FG-01", "LOT-A")] = stock
            });

        Assert.Contains("không đủ tồn", error);
    }

    [Fact]
    public void Validate_RejectsLotOutsideScopedInventory()
    {
        var requested = new[] { new DeliveryOrderRequestedLot(Guid.NewGuid(), "FG-01", "OTHER-COMPANY", 1m) };

        var error = DeliveryOrderLotInventoryRules.Validate(
            requested,
            new Dictionary<string, DeliveryOrderLotInventorySnapshot>());

        Assert.Contains("không hợp lệ", error);
    }

    [Fact]
    public void Validate_RejectsLotBelongingToAnotherProduct()
    {
        var requested = new[] { new DeliveryOrderRequestedLot(Guid.NewGuid(), "FG-01", "LOT-B", 1m) };
        var otherProductStock = Snapshot(Guid.NewGuid(), "FG-02", "LOT-B", available: 10m, productAvailable: 10m);

        var error = DeliveryOrderLotInventoryRules.Validate(
            requested,
            new Dictionary<string, DeliveryOrderLotInventorySnapshot>
            {
                [DeliveryOrderLotInventoryRules.Key("FG-02", "LOT-B")] = otherProductStock
            });

        Assert.Contains("không hợp lệ", error);
    }

    [Fact]
    public void Validate_RejectsAggregateAboveProductAvailability()
    {
        var productId = Guid.NewGuid();
        var requested = new[]
        {
            new DeliveryOrderRequestedLot(productId, "FG-01", "LOT-A", 6m),
            new DeliveryOrderRequestedLot(productId, "FG-01", "LOT-B", 6m)
        };
        var inventory = new Dictionary<string, DeliveryOrderLotInventorySnapshot>
        {
            [DeliveryOrderLotInventoryRules.Key("FG-01", "LOT-A")] = Snapshot(productId, "FG-01", "LOT-A", 10m, 10m),
            [DeliveryOrderLotInventoryRules.Key("FG-01", "LOT-B")] = Snapshot(productId, "FG-01", "LOT-B", 10m, 10m)
        };

        var error = DeliveryOrderLotInventoryRules.Validate(requested, inventory);

        Assert.Contains("Product FG-01 không đủ", error);
    }

    [Fact]
    public void CalculateTotalCost_RoundsToStoredPrecision()
    {
        Assert.Equal(3.34m, DeliveryOrderLotInventoryRules.CalculateTotalCost(1.005m, 3.325m));
    }

    private static DeliveryOrderLotInventorySnapshot Snapshot(
        Guid productId,
        string productCode,
        string lotNo,
        decimal available,
        decimal productAvailable)
        => new(
            productId, productCode, lotNo, available, 0m, available, productAvailable, 2m);
}
