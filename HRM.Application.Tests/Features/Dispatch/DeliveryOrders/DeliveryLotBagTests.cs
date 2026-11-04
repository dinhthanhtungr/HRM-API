using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.PatchDeliveryOrder;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryLotBagTests
{
    [Theory]
    [InlineData(2, 3, true)]
    [InlineData(2, 2, false)]
    [InlineData(-1, 6, false)]
    [InlineData(null, 5, false)]
    public void Normalize_ValidatesAndPersistsBagsForEachType(int? goodBags, int defectiveBags, bool expected)
    {
        var id = Guid.NewGuid();
        var ok = DeliveryOrderLotNormalizer.TryNormalize([new DeliveryOrderLineRequest
        {
            MerchandiseOrderDetailId = id, Quantity = 269, NumOfBags = 5,
            Lots = [new() { LotNo = "A", StockType = "FinishedGood", Quantity = 57, NumOfBags = goodBags },
                new() { LotNo = "a", StockType = "DefectiveFinishedGood", Quantity = 212, NumOfBags = defectiveBags }]
        }], out var lines, out _);
        Assert.Equal(expected, ok);
        if (!ok) return;
        var rows = DeliveryWarehouseRequestService.BuildTypedDetails(new DeliveryOrder
        {
            Details = [new() { MerchandiseOrderDetailId = id, ProductExternalIdSnapShot = "P" }]
        }, lines);
        Assert.Equal(2, rows.Single(x => x.ItemStockType == StockType.FinishedGood).BagNumber);
        Assert.Equal(3, rows.Single(x => x.ItemStockType == StockType.DefectiveFinishedGood).BagNumber);
    }

    [Theory]
    [InlineData(2, 3, true)]
    [InlineData(2, 4, false)]
    public void Patch_PreservesQuantityAndType_AndChecksProductBagTotal(int good, int defective, bool expected)
    {
        WarehouseRequestDetail[] rows = [new() { ProductCode = "P", LotNumber = "A", ItemStockType = StockType.FinishedGood, WeightKg = 57 },
            new() { ProductCode = "P", LotNumber = "a", ItemStockType = StockType.DefectiveFinishedGood, WeightKg = 212 }];
        DeliveryOrderDetail[] details = [new() { Id = Guid.NewGuid(), ProductExternalIdSnapShot = "P", NumOfBags = 5 }];
        var error = DeliveryLotBagPatchRules.Apply(rows, details,
            [new("P", "A", "FinishedGood", good), new("P", "a", "DefectiveFinishedGood", defective)], [details[0].Id]);
        Assert.Equal(expected, error is null);
        Assert.Equal(57, rows[0].WeightKg);
        Assert.Equal(212, rows[1].WeightKg);
        if (expected) { Assert.Equal(good, rows[0].BagNumber); Assert.Equal(defective, rows[1].BagNumber); }
    }

    [Fact]
    public void Patch_RejectsUnknownLotAndAmbiguousLegacyTotal()
    {
        WarehouseRequestDetail[] rows = [new() { ProductCode = "P", LotNumber = "A" }, new() { ProductCode = "P", LotNumber = "B" }];
        DeliveryOrderDetail[] details = [new() { Id = Guid.NewGuid(), ProductExternalIdSnapShot = "P", NumOfBags = 5 }];
        Assert.NotNull(DeliveryLotBagPatchRules.Apply(rows, details, null, [details[0].Id]));
        Assert.NotNull(DeliveryLotBagPatchRules.Apply(rows, details, [new("P", "C", "FinishedGood", 5)], [details[0].Id]));
        Assert.NotNull(DeliveryLotBagPatchRules.Apply(rows, details, [new("P", "A", "RawMaterial", 5)], [details[0].Id]));
    }
}
