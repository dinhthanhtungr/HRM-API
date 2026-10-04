using HRM.Application.Features.Dispatch.DeliveryOrders;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderAvailableQuantityTests
{
    [Fact]
    public void MultipleLots_DoNotMultiplyProductAvailabilityAfterUnassignedReserve()
    {
        // Hai lot 10kg, reserve chưa gắn lot 12kg: tổng chỉ còn 8kg, không phải 16kg.
        var lots = new[] { Lot("A", 10m, 8m), Lot("B", 10m, 8m) };
        Assert.Equal(8m, DeliveryOrderLotInventoryRules.CalculateAvailableQuantity(lots));
    }

    [Fact]
    public void LotAvailability_StillLimitsProductTotal()
    {
        var lots = new[] { Lot("A", 2m, 20m), Lot("B", 3m, 20m) };
        Assert.Equal(5m, DeliveryOrderLotInventoryRules.CalculateAvailableQuantity(lots));
    }

    [Fact]
    public void EmptyOrExhaustedStock_ReturnsZero()
    {
        Assert.Equal(0m, DeliveryOrderLotInventoryRules.CalculateAvailableQuantity([]));
        Assert.Equal(0m, DeliveryOrderLotInventoryRules.CalculateAvailableQuantity([Lot("A", 10m, 0m)]));
    }

    private static DeliveryOrderLotInventorySnapshot Lot(string lot, decimal available, decimal productAvailable)
        => new(Guid.Empty, "PRODUCT", lot, available, 0m, available, productAvailable, 0m);
}
