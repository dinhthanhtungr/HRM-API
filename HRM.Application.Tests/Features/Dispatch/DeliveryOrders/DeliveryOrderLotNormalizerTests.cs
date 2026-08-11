using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderLotNormalizerTests
{
    [Fact]
    public void TryNormalize_AcceptsLegacyLotNoListAsSingleLot()
    {
        var detailId = Guid.NewGuid();

        var success = DeliveryOrderLotNormalizer.TryNormalize(
            [
                new DeliveryOrderLineRequest
                {
                    MerchandiseOrderDetailId = detailId,
                    Quantity = 12.5m,
                    NumOfBags = 2,
                    LotNoList = " LOT-01 "
                }
            ],
            out var lines,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        var line = Assert.Single(lines);
        Assert.Equal(12.5m, line.Quantity);
        Assert.Equal("LOT-01", line.LotNoList);
        var lot = Assert.Single(line.Lots);
        Assert.Equal("LOT-01", lot.LotNo);
        Assert.Equal(12.5m, lot.Quantity);
    }

    [Fact]
    public void TryNormalize_AcceptsMultipleLotsAndGeneratesLegacyDisplay()
    {
        var success = DeliveryOrderLotNormalizer.TryNormalize(
            [CreateLine(10m, ("LOT-A", 4m), ("LOT-B", 6m))],
            out var lines,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        var line = Assert.Single(lines);
        Assert.Equal(10m, line.Quantity);
        Assert.Equal("LOT-A, LOT-B", line.LotNoList);
        Assert.Collection(
            line.Lots,
            lot => Assert.Equal(("LOT-A", 4m), (lot.LotNo, lot.Quantity)),
            lot => Assert.Equal(("LOT-B", 6m), (lot.LotNo, lot.Quantity)));
    }

    [Fact]
    public void TryNormalize_RejectsQuantityDifferentFromLotTotal()
    {
        var success = DeliveryOrderLotNormalizer.TryNormalize(
            [CreateLine(10m, ("LOT-A", 4m), ("LOT-B", 5m))],
            out var lines,
            out var error);

        Assert.False(success);
        Assert.Empty(lines);
        Assert.Contains("tổng quantity", error);
    }

    [Fact]
    public void TryNormalize_DoesNotFallbackWhenLotsIsExplicitlyEmpty()
    {
        var success = DeliveryOrderLotNormalizer.TryNormalize(
            [
                new DeliveryOrderLineRequest
                {
                    MerchandiseOrderDetailId = Guid.NewGuid(),
                    Quantity = 10m,
                    LotNoList = "LEGACY-LOT",
                    Lots = []
                }
            ],
            out var lines,
            out var error);

        Assert.False(success);
        Assert.Empty(lines);
        Assert.Contains("ít nhất một lot", error);
    }

    [Fact]
    public void TryNormalize_PrefersLotsOverLegacyDisplayValue()
    {
        var line = CreateLine(10m, ("LOT-A", 10m));
        line = new DeliveryOrderLineRequest
        {
            MerchandiseOrderDetailId = line.MerchandiseOrderDetailId,
            Quantity = line.Quantity,
            NumOfBags = line.NumOfBags,
            LotNoList = "STALE-LEGACY-VALUE",
            Lots = line.Lots
        };

        var success = DeliveryOrderLotNormalizer.TryNormalize(
            [line],
            out var lines,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("LOT-A", Assert.Single(lines).LotNoList);
    }

    [Fact]
    public void TryNormalize_MergesDuplicateLotsCaseInsensitively()
    {
        var detailId = Guid.NewGuid();

        var success = DeliveryOrderLotNormalizer.TryNormalize(
            [
                CreateLine(detailId, 4m, ("LOT-A", 4m)),
                CreateLine(detailId, 6m, ("lot-a", 1m), ("LOT-B", 5m))
            ],
            out var lines,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        var line = Assert.Single(lines);
        Assert.Equal(10m, line.Quantity);
        Assert.Collection(
            line.Lots,
            lot => Assert.Equal(("LOT-A", 5m), (lot.LotNo, lot.Quantity)),
            lot => Assert.Equal(("LOT-B", 5m), (lot.LotNo, lot.Quantity)));
    }

    [Fact]
    public void LotRequest_DoesNotExposeCostSnapshotProperties()
    {
        var propertyNames = typeof(DeliveryOrderLotRequest)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.Equal(2, propertyNames.Length);
        Assert.Contains("LotNo", propertyNames);
        Assert.Contains("Quantity", propertyNames);
        Assert.DoesNotContain("UnitCostSnapshot", propertyNames);
        Assert.DoesNotContain("TotalCostSnapshot", propertyNames);
    }

    private static DeliveryOrderLineRequest CreateLine(
        decimal quantity,
        params (string LotNo, decimal Quantity)[] lots)
        => CreateLine(Guid.NewGuid(), quantity, lots);

    private static DeliveryOrderLineRequest CreateLine(
        Guid merchandiseOrderDetailId,
        decimal quantity,
        params (string LotNo, decimal Quantity)[] lots)
        => new()
        {
            MerchandiseOrderDetailId = merchandiseOrderDetailId,
            Quantity = quantity,
            NumOfBags = 1,
            Lots = lots.Select(x => new DeliveryOrderLotRequest
            {
                LotNo = x.LotNo,
                Quantity = x.Quantity
            }).ToArray()
        };
}
