using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.WareHouses;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingFormulaStockReaderTests
{
    [Fact]
    public async Task ProductLots_ExcludeForeignAndQcShelfStocksAndPreserveWaiterStatus()
    {
        var f = new Fixture();
        var product = new HRM.Domain.Entities.SampleRequestSchema.Product { ProductId = Guid.NewGuid(), CompanyId = f.Company, IsActive = true, ColourCode = "M" };
        f.Store.Seed(nameof(f.Db.Products), product);
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), f.Stock(1, 2, "WAIT", StockType.Waiter),
            f.Stock(2, 3, "WAIT", StockType.Waiter), f.Stock(3, 4, "QC", slot: "CT.0.1"),
            f.Stock(4, 900, "FOREIGN", company: Guid.NewGuid()), f.Stock(5, 0, "EMPTY"));
        var row = Assert.Single(await ManufacturingProductStockReader.ReadAsync(f.Db, f.Company, product.ProductId, default));
        Assert.Equal("WAIT", row.LotNo);
        Assert.Equal(5m, row.QuantityKg);
        Assert.Equal("PendingQC", row.Status);
        Assert.Null(row.ManufacturingDate);
        Assert.Equal(6, (int)StockType.Waiter);
        Assert.Equal(7, (int)StockType.Material);
        Assert.Empty(await ManufacturingProductStockReader.ReadAsync(f.Db, Guid.NewGuid(), product.ProductId, default));
    }

    [Fact]
    public async Task Stock_GroupsShelfQuantitiesAndGrossOpenReservationsWithinCompany()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), f.Stock(1, 10, "A"), f.Stock(2, 5, "A"),
            f.Stock(3, 7, "B", slot: "CT.0.1"), f.Stock(4, 1000, "foreign", company: Guid.NewGuid()));
        f.Store.Seed(nameof(f.Db.WarehouseTempStocks), new WarehouseTempStock { CompanyId = f.Company, Code = "M", ReserveStatus = "Open", QtyRequest = 30, QtyUsed = 10 },
            new WarehouseTempStock { CompanyId = Guid.NewGuid(), Code = "M", ReserveStatus = "Open", QtyRequest = 900 },
            new WarehouseTempStock { CompanyId = f.Company, Code = "M", ReserveStatus = "Consumed", QtyRequest = 900 });
        var row = Assert.Single(await ManufacturingFormulaStockReader.FillAsync(f.Db, f.Company, [f.Item()], default));
        Assert.Equal(15m, row.OnHandKg);
        Assert.Equal(30m, row.ReservedOpenAllKg);
        Assert.Equal(-15m, row.AvailableKg);
        var lot = Assert.Single(row.Lots);
        Assert.Equal("A", lot.LotNo);
        Assert.Equal(15m, lot.QuantityKg);
    }

    [Fact]
    public async Task Lots_InsertPassPlaceholderAndKeepOnlyPersistedLotAfterCurrentOptions()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), f.Stock(1, 5, "FAIL", StockType.DefectiveRawMaterial));
        var rows = await ManufacturingFormulaStockReader.FillAsync(f.Db, f.Company, [f.Item(), f.Item("OLD")], default, canViewDefectiveLots: true);
        Assert.Equal("N/A", rows[0].Lots[0].LotNo);
        Assert.Equal(StockType.RawMaterial, rows[0].Lots[0].StockType);
        Assert.True(rows[0].Lots[1].IsDefective);
        Assert.Equal("OLD", rows[1].Lots[1].LotNo);
        Assert.Equal("Unknown", rows[1].Lots[1].QualityStatus);
        Assert.Equal(0, rows[1].Lots[1].QuantityKg);
    }

    [Fact]
    public async Task Lots_OrderPassBeforeDefectiveAndOldestBeforeNewest()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), f.Stock(3, 1, "FAIL", StockType.DefectiveRawMaterial),
            f.Stock(2, 1, "NEW"), f.Stock(1, 1, "OLD"));
        var row = Assert.Single(await ManufacturingFormulaStockReader.FillAsync(f.Db, f.Company, [f.Item("NEW")], default, canViewDefectiveLots: true));
        Assert.Equal(new[] { "OLD", "NEW", "FAIL" }, row.Lots.Select(x => x.LotNo));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lots_OnlyVisiblePositiveStockAndSavedQualityAreReturned(bool allowed)
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), f.Stock(1, 5, "FAIL", StockType.DefectiveRawMaterial),
            f.Stock(2, 3, "PASS"), f.Stock(3, 0, "ZERO"), f.Stock(4, 5, "WAIT", StockType.Waiter),
            f.Stock(5, 5, "QC", slot: "CT.0.1"));
        var savedDefective = f.Item("SAVED-FAIL") with { ItemType = ItemType.MaterialFailure };
        var rows = await ManufacturingFormulaStockReader.FillAsync(f.Db, f.Company, [f.Item(), savedDefective], default, allowed);
        Assert.Equal(allowed ? new[] { "PASS", "FAIL" } : new[] { "PASS" }, rows[0].Lots.Select(x => x.LotNo));
        Assert.Equal(allowed ? "SAVED-FAIL" : null, rows[1].LotNo);
        Assert.Equal(allowed, rows[1].Lots.Any(x => x.LotNo == "SAVED-FAIL" && x.IsDefective));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lots_NoPassingStockAlwaysHasPassingNa(bool allowed)
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), f.Stock(1, 5, "FAIL", StockType.DefectiveRawMaterial));
        var row = Assert.Single(await ManufacturingFormulaStockReader.FillAsync(f.Db, f.Company, [f.Item()], default, allowed));
        Assert.Equal(allowed ? 2 : 1, row.Lots.Count);
        Assert.Equal("N/A", row.Lots[0].LotNo);
        Assert.Equal(StockType.RawMaterial, row.Lots[0].StockType);
        Assert.Equal("Pass", row.Lots[0].QualityStatus);
        Assert.False(row.Lots[0].IsDefective);
    }

    private sealed class Fixture
    {
        public Guid Company { get; } = Guid.NewGuid();
        public IManufacturingDbContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public Fixture() => (Db, Store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        public ProductionOrderFormulaSourceItemDto Item(string? lot = null) => new(Guid.NewGuid(), Guid.NewGuid(), ItemType.Material,
            Guid.NewGuid(), 1, 1m, null, "material", "M", "kg", lot);
        public WarehouseShelfStock Stock(int id, decimal qty, string lot, StockType type = StockType.RawMaterial, string slot = "S", Guid? company = null)
            => new() { ShelfStockId = id, Code = "M", LotNo = lot, QtyKg = qty, StockType = type, CompanyId = company ?? Company,
                UpdatedDate = new DateTime(2026, 1, 1).AddDays(id), WarehouseShelves = new() {
                    CompanyId = company ?? Company, SlotCode = slot, IsActive = true } };
    }
}
