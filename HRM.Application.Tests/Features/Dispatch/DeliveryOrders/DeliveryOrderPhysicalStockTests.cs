using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderPhysicalStockTests
{
    [Fact]
    public async Task Inventory_IgnoresLotAndUnassignedReserves_ButStillRejectsOverDelivery()
    {
        var (db, store) = IqcTestStore.CreateContext<IDispatchReadDbContext>();
        var company = Guid.NewGuid();
        var product = Guid.NewGuid();
        var shelf = new WarehouseShelves { CompanyId = company, IsActive = true };
        store.Seed(nameof(db.WarehouseShelfStocks),
            new WarehouseShelfStock { CompanyId = company, Code = "P", LotNo = "VA-A", LotKey = "VA-A", StockType = StockType.FinishedGood, QtyKg = 10m, WarehouseShelves = shelf },
            new WarehouseShelfStock { CompanyId = company, Code = "P", LotNo = "VA-B", LotKey = "VA-B", StockType = StockType.FinishedGood, QtyKg = 5m, WarehouseShelves = shelf },
            new WarehouseShelfStock { CompanyId = company, Code = "P", LotNo = "VA-A", StockType = StockType.DefectiveFinishedGood, QtyKg = 3m, WarehouseShelves = shelf });
        store.Seed(nameof(db.WarehouseTempStocks),
            new WarehouseTempStock { CompanyId = company, Code = "P", LotKey = "VA-A", ReserveStatus = "Open", QtyRequest = 100m },
            new WarehouseTempStock { CompanyId = company, Code = "P", ReserveStatus = "Open", QtyRequest = 200m });

        var inventory = await new DeliveryOrderLotInventoryService(db).LoadAsync(company, [(product, "P")], false, CancellationToken.None);
        var good = inventory[DeliveryOrderLotInventoryRules.Key("P", "VA-A")];
        Assert.Equal(100m, good.ReservedQuantity); // Informational field is preserved.
        Assert.Equal(10m, good.AvailableQuantity);
        Assert.Equal(15m, good.ProductAvailableQuantity);
        Assert.Equal(18m, DeliveryOrderLotInventoryRules.CalculateAvailableQuantity(inventory.Values));
        Assert.Null(DeliveryOrderLotInventoryRules.Validate([new(product, "P", "VA-A", 10m)], inventory));
        Assert.NotNull(DeliveryOrderLotInventoryRules.Validate([new(product, "P", "VA-A", 11m)], inventory));
        Assert.NotNull(DeliveryOrderLotInventoryRules.Validate([new(product, "P", "VA-A", 4m, StockType.DefectiveFinishedGood)], inventory));
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }
}
