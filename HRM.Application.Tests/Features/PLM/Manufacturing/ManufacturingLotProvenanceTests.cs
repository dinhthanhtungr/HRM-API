using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderFormulaSource;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingLotProvenanceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FormulaSource_KeepsSavedLotForBothSelectedAndCopySource(bool selectedForCurrentOrder, bool hasStock)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Standard);
        f.Order.Status = ManufacturingProductOrder.Scheduling.ToString();
        f.User.Roles = [ApplicationRoles.Developer];
        f.Store.Seed(nameof(f.Db.MfgProductionOrders), f.Order);
        var va = f.Db.ManufacturingFormulas.Single();
        var row = f.Db.ManufacturingFormulaMaterials.Single();
        row.LotNo = "OLD-SAVED";
        if (hasStock)
        {
            f.Store.Seed(nameof(f.Db.WarehouseShelfStocks),
                new HRM.Domain.Entities.WarehouseSchema.WarehouseShelfStock
                {
                    ShelfStockId = 1, CompanyId = f.Order.CompanyId, Code = f.Material.ExternalId!,
                    LotNo = "FIFO", QtyKg = 10, StockType = HRM.Domain.Enums.WareHouses.StockType.RawMaterial,
                    WarehouseShelves = new() { CompanyId = f.Order.CompanyId, IsActive = true, SlotCode = "S" }
                });
        }
        var otherOrder = new MfgProductionOrder { MfgProductionOrderId = Guid.NewGuid(), CompanyId = f.Order.CompanyId,
            ProductId = f.Order.ProductId, IsActive = true };
        var parent = selectedForCurrentOrder ? f.Order : otherOrder;
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), new ProductionSelectVersion
        {
            CompanyId = f.Order.CompanyId, MfgProductionOrderId = parent.MfgProductionOrderId, MfgProductionOrder = parent,
            ManufacturingFormulaId = va.ManufacturingFormulaId, ManufacturingFormula = va
        });
        var permissions = new CurrentUserPermissionService(f.User);
        var (displayDb, displayStore) = ProductionOrderTestStore.CreateContext<HRM.Application.Abstractions.Persistence.PLM.IPLMReadDbContext>();
        displayStore.Seed(nameof(displayDb.Materials), f.Material);
        var handler = new GetProductionOrderFormulaSourceQueryHandler(f.Db, displayDb, f.User, permissions, new PLMFieldVisibilityService(permissions));
        var result = await handler.Handle(new(f.Order.MfgProductionOrderId, va.ManufacturingFormulaId), default);
        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("OLD-SAVED", item.LotNo);
        Assert.Contains(item.Lots, x => x.LotNo == "OLD-SAVED");
        Assert.Equal(hasStock ? "FIFO" : "N/A", item.Lots[0].LotNo);
        Assert.Equal("Pass", item.Lots[0].QualityStatus);
        f.Order.CompanyId = Guid.NewGuid();
        Assert.False((await handler.Handle(new(f.Order.MfgProductionOrderId, va.ManufacturingFormulaId), default)).Success);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NewSource_UsesOldestGoodStockOrGoodNa_WithoutChangingSavedFormula(bool hasGoodStock, bool sourceDefective)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Standard);
        f.User.Roles = [ApplicationRoles.Developer];
        f.Order.Status = ManufacturingProductOrder.New.ToString();
        f.Store.Seed(nameof(f.Db.MfgProductionOrders), f.Order);
        var row = f.Db.ManufacturingFormulaMaterials.Single();
        row.LotNo = "OLD-SAVED";
        row.itemType = sourceDefective ? HRM.Domain.Enums.Formulas.ItemType.MaterialFailure : HRM.Domain.Enums.Formulas.ItemType.Material;
        HRM.Domain.Entities.WarehouseSchema.WarehouseShelfStock Stock(int id, string lot, int age, HRM.Domain.Enums.WareHouses.StockType type) => new()
        {
            ShelfStockId = id, CompanyId = f.Order.CompanyId, Code = f.Material.ExternalId!,
            LotNo = lot, QtyKg = 10, StockType = type, UpdatedDate = new DateTime(2026, 1, 1).AddDays(age),
            WarehouseShelves = new() { CompanyId = f.Order.CompanyId, IsActive = true, SlotCode = "S" }
        };
        var stocks = new List<HRM.Domain.Entities.WarehouseSchema.WarehouseShelfStock>
        {
            Stock(1, "DEFECTIVE-OLDEST", 0, HRM.Domain.Enums.WareHouses.StockType.DefectiveRawMaterial)
        };
        if (hasGoodStock)
        {
            stocks.Add(Stock(3, "A-NEWER", 3, HRM.Domain.Enums.WareHouses.StockType.RawMaterial));
            stocks.Add(Stock(2, "Z-FIFO", 2, HRM.Domain.Enums.WareHouses.StockType.RawMaterial));
            var empty = Stock(4, "EMPTY", 0, HRM.Domain.Enums.WareHouses.StockType.RawMaterial);
            empty.QtyKg = 0;
            stocks.Add(empty);
            var inactive = Stock(5, "INACTIVE", 0, HRM.Domain.Enums.WareHouses.StockType.RawMaterial);
            inactive.WarehouseShelves!.IsActive = false;
            stocks.Add(inactive);
            var foreign = Stock(6, "FOREIGN", 0, HRM.Domain.Enums.WareHouses.StockType.RawMaterial);
            foreign.CompanyId = Guid.NewGuid();
            stocks.Add(foreign);
        }
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), stocks.ToArray());
        var permissions = new CurrentUserPermissionService(f.User);
        var (displayDb, displayStore) = ProductionOrderTestStore.CreateContext<HRM.Application.Abstractions.Persistence.PLM.IPLMReadDbContext>();
        displayStore.Seed(nameof(displayDb.Materials), f.Material);
        var handler = new GetProductionOrderFormulaSourceQueryHandler(f.Db, displayDb, f.User, permissions, new PLMFieldVisibilityService(permissions));
        var result = await handler.Handle(new(f.Order.MfgProductionOrderId, row.ManufacturingFormulaId), default);
        Assert.True(result.Success, result.Message);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(hasGoodStock ? "Z-FIFO" : "N/A", item.LotNo);
        Assert.Equal(HRM.Domain.Enums.Formulas.ItemType.Material, item.ItemType);
        Assert.Equal("Pass", item.Lots.First(x => x.LotNo == item.LotNo).QualityStatus);
        Assert.DoesNotContain(item.Lots, x => x.LotNo == "OLD-SAVED");
        Assert.Equal("OLD-SAVED", row.LotNo);
        Assert.Equal(sourceDefective ? HRM.Domain.Enums.Formulas.ItemType.MaterialFailure : HRM.Domain.Enums.Formulas.ItemType.Material, row.itemType);
    }

    [Theory]
    [InlineData(ApplicationRoles.Developer, true)]
    [InlineData(ApplicationRoles.Quality.QCUser, true)]
    [InlineData(ApplicationRoles.Admin, false)]
    [InlineData(ApplicationRoles.President, false)]
    [InlineData(ApplicationRoles.Production.PLPUUser, false)]
    [InlineData(ApplicationRoles.Lab.LabUser, true)]
    public void DefectiveLotCapability_DeveloperQcAndLabByDefault(string role, bool allowed)
    {
        var user = new ProductionOrderCreationTests.TestUser { Roles = [role] };
        var visibility = new PLMFieldVisibilityService(new CurrentUserPermissionService(user));
        Assert.Equal(allowed, visibility.CanViewManufacturingDefectiveLots());
        user.IsAuthenticated = false;
        Assert.False(visibility.CanViewManufacturingDefectiveLots());
    }

    [Fact]
    public void ExplicitPermissions_FailClosedUntilNewClaimIsGranted()
    {
        var user = new ProductionOrderCreationTests.TestUser { Roles = [ApplicationRoles.Developer], HasExplicitPermissionSet = true };
        var visibility = new PLMFieldVisibilityService(new CurrentUserPermissionService(user));
        Assert.False(visibility.CanViewManufacturingDefectiveLots());
        user.Permissions = [ApplicationPermissions.PLM.ViewManufacturingDefectiveLots];
        Assert.True(visibility.CanViewManufacturingDefectiveLots());
    }
}
