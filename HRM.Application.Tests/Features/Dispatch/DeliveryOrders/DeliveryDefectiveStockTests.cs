using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryDefectiveStockTests
{
    [Fact]
    public void Normalize_AndWarehouseRequest_PreserveMixedTypeSameLot_WithoutSchemaChange()
    {
        var id = Guid.NewGuid();
        Assert.True(DeliveryOrderLotNormalizer.TryNormalize([new DeliveryOrderLineRequest
        {
            MerchandiseOrderDetailId = id, Quantity = 266.5m, NumOfBags = 3,
            Lots = [new() { LotNo = "VA260700418", StockType = "FinishedGood", Quantity = 57m },
                new() { LotNo = "va260700418", StockType = "DefectiveFinishedGood", Quantity = 209.5m }]
        }], out var lines, out var error), error);
        Assert.Equal(2, lines[0].Lots.Count);
        Assert.Equal("VA260700418", lines[0].LotNoList);
        var order = new DeliveryOrder { Details = [new DeliveryOrderDetail
        {
            MerchandiseOrderDetailId = id, ProductExternalIdSnapShot = "P", Quantity = 266.5m, NumOfBags = 3
        }] };
        var rows = DeliveryWarehouseRequestService.BuildTypedDetails(order, lines);
        Assert.Equal(2, rows.Count);
        Assert.Equal(57m, rows.Single(x => x.ItemStockType == StockType.FinishedGood).WeightKg);
        Assert.Equal(209.5m, rows.Single(x => x.ItemStockType == StockType.DefectiveFinishedGood).WeightKg);
        Assert.Equal(3, rows.Sum(x => x.BagNumber));
    }

    [Theory]
    [InlineData(57, 209.5, true)]
    [InlineData(58, 208.5, false)]
    [InlineData(56, 210.5, false)]
    public void Inventory_ValidatesTypesIndependently_IncludingCaseInsensitiveLot(decimal good, decimal defective, bool valid)
    {
        var id = Guid.NewGuid();
        var inventory = new Dictionary<string, DeliveryOrderLotInventorySnapshot>
        {
            [DeliveryOrderLotInventoryRules.Key("P", "VA260700418")] = new(id, "P", "VA260700418", 57, 0, 57, 57, 0),
            [DeliveryOrderLotInventoryRules.Key("P", "va260700418", StockType.DefectiveFinishedGood)] =
                new(id, "P", "va260700418", 209.5m, 0, 209.5m, 209.5m, 0, StockType.DefectiveFinishedGood)
        };
        var error = DeliveryOrderLotInventoryRules.Validate([new(id, "P", "VA260700418", good),
            new(id, "P", "va260700418", defective, StockType.DefectiveFinishedGood)], inventory);
        Assert.Equal(valid, error is null);
        Assert.Equal(266.5m, DeliveryOrderLotInventoryRules.CalculateAvailableQuantity(inventory.Values));
    }

    [Fact]
    public void Inventory_AggregatesSameTypeAcrossPoLines_AndRejectsMissingDefectiveStock()
    {
        var id = Guid.NewGuid();
        var inventory = new Dictionary<string, DeliveryOrderLotInventorySnapshot>
        { [DeliveryOrderLotInventoryRules.Key("P", "A")] = new(id, "P", "A", 57, 0, 57, 57, 0) };
        Assert.NotNull(DeliveryOrderLotInventoryRules.Validate([new(id, "P", "A", 30), new(id, "P", "a", 28)], inventory));
        Assert.NotNull(DeliveryOrderLotInventoryRules.Validate([new(id, "P", "A", 1, StockType.DefectiveFinishedGood)], inventory));
    }
}
