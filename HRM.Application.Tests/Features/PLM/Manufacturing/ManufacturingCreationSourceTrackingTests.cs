using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingCreationSourceTrackingTests
{
    [Theory]
    [InlineData("LabUser", true)]
    [InlineData("PLPUUser", false)]
    public async Task DefectiveMaterialInput_RequiresDefectiveLotCapability(string role, bool allowed)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Production);
        f.User.Roles = [role];
        var source = f.Db.ManufacturingFormulaMaterials.Single();
        var resolver = new ManufacturingFormulaInputResolver(f.Db, null!, new ManufacturingFormulaCreationTests.Clock(),
            new HRM.Application.Commons.Authorization.CurrentUserPermissionService(f.User));
        var result = await resolver.ResolveAsync(f.Order, new(f.SourceId, "VA", ManufacturingFormulaOrigin.Production,
            null, DateTime.Now, false), [new() { SourceItemId = source.ManufacturingFormulaMaterialId,
            ItemId = f.Material.MaterialId, ItemType = ItemType.MaterialFailure, Quantity = 1, LotNo = "N/A" }], default);
        Assert.Equal(allowed, result.Success);
        if (!allowed) Assert.Contains("quyền chọn hàng lỗi", result.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherResolverCallers_RetainStrictSourceTracking(bool repeated)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Production);
        var source = f.Db.ManufacturingFormulaMaterials.Single();
        var resolver = new ManufacturingFormulaInputResolver(f.Db, null!, new ManufacturingFormulaCreationTests.Clock(), new HRM.Application.Commons.Authorization.CurrentUserPermissionService(f.User));
        var rows = new List<HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders.ManufacturingFormulaInputItem> {
            new() { SourceItemId = repeated ? source.ManufacturingFormulaMaterialId : Guid.NewGuid(),
                ItemId = f.Material.MaterialId, ItemType = ItemType.Material, Quantity = repeated ? .5m : 1 } };
        if (repeated) rows.Add(new() { SourceItemId = source.ManufacturingFormulaMaterialId,
            ItemId = f.Material.MaterialId, ItemType = ItemType.Material, Quantity = .5m });
        var result = await resolver.ResolveAsync(f.Order, new(f.SourceId, "VA", ManufacturingFormulaOrigin.Production,
            null, ManufacturingFormulaCreationTests.Fixture.Now, true), rows, default);
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_MissingOrUnrelatedSourceIdUsesOnlyUniqueScopedSource(bool unrelated)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Production);
        f.User.Roles = [HRM.Application.Commons.Authorization.ApplicationRoles.Developer];
        var source = f.Db.ManufacturingFormulaMaterials.Single();
        source.LotNo = "SAVED-EMPTY";
        source.itemType = ItemType.MaterialFailure;
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request(items: [new() {
            SourceItemId = unrelated ? Guid.NewGuid() : null,
            ItemId = f.Material.MaterialId, ItemType = ItemType.MaterialFailure,
            Quantity = 1, LotNo = "SAVED-EMPTY" }])), default);
        Assert.True(result.Success, result.Message);
        var copied = Assert.Single(Assert.Single(f.Store.Added.OfType<ManufacturingFormula>()).ManufacturingFormulaMaterials);
        Assert.Equal(source.UnitPrice, copied.UnitPrice);
        Assert.Equal(source.LotNo, copied.LotNo);
        Assert.Equal(ItemType.MaterialFailure, copied.itemType);
        Assert.DoesNotContain("Thêm:", f.Order.PlpuNote ?? "");
        Assert.DoesNotContain("Xóa:", f.Order.PlpuNote ?? "");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_RepeatedSourceIdDoesNotReplaceMaterialLotDuplicateRule(bool sameLot)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Production);
        var source = f.Db.ManufacturingFormulaMaterials.Single();
        source.CategoryId = f.Material.CategoryId = f.Db.Categories.Single().CategoryId
            = Guid.Parse("04fbe69b-dbd1-45ee-9361-50031955681b");
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), Stock(f, "A"), Stock(f, "B"));
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request(items: [new() {
            SourceItemId = source.ManufacturingFormulaMaterialId, ItemId = f.Material.MaterialId,
            ItemType = ItemType.Material, Quantity = .5m, LotNo = "A" }, new() {
            SourceItemId = source.ManufacturingFormulaMaterialId, ItemId = f.Material.MaterialId,
            ItemType = ItemType.Material, Quantity = .5m, LotNo = sameLot ? "A" : "B" }])), default);
        Assert.Equal(!sameLot, result.Success);
        if (!sameLot) Assert.Equal(2, Assert.Single(f.Store.Added.OfType<ManufacturingFormula>()).ManufacturingFormulaMaterials.Count);
        else Assert.Empty(f.Store.Added);
    }

    [Fact]
    public async Task Create_OptionalSourceIdCannotBypassChangedLotValidation()
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Production);
        var source = f.Db.ManufacturingFormulaMaterials.Single();
        source.LotNo = "SAVED";
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request(items: [new() {
            ItemId = f.Material.MaterialId, ItemType = ItemType.Material, Quantity = 1, LotNo = "FORGED" }])), default);
        Assert.False(result.Success);
        Assert.Empty(f.Store.Added);
        Assert.False(f.Store.Transaction.Committed);
    }

    [Fact]
    public async Task Create_SameMaterialAndLotCanContainGoodAndDefectiveRows()
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Production);
        f.User.Roles = [HRM.Application.Commons.Authorization.ApplicationRoles.Developer];
        var source = f.Db.ManufacturingFormulaMaterials.Single();
        source.CategoryId = f.Material.CategoryId = f.Db.Categories.Single().CategoryId
            = Guid.Parse("04fbe69b-dbd1-45ee-9361-50031955681b");
        var good = Stock(f, "A");
        var defective = Stock(f, "A");
        defective.StockType = StockType.DefectiveRawMaterial;
        f.Store.Seed(nameof(f.Db.WarehouseShelfStocks), good, defective);
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request(items: [new() {
            SourceItemId = source.ManufacturingFormulaMaterialId, ItemId = f.Material.MaterialId,
            ItemType = ItemType.Material, Quantity = .5m, LotNo = "A" }, new() {
            SourceItemId = source.ManufacturingFormulaMaterialId, ItemId = f.Material.MaterialId,
            ItemType = ItemType.MaterialFailure, Quantity = .5m, LotNo = "A" }])), default);
        Assert.True(result.Success, result.Message);
        var rows = Assert.Single(f.Store.Added.OfType<ManufacturingFormula>()).ManufacturingFormulaMaterials;
        Assert.Contains(rows, x => x.itemType == ItemType.Material);
        Assert.Contains(rows, x => x.itemType == ItemType.MaterialFailure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_CannotCopyDefectiveLotWithoutCapability(bool explicitItems)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Production);
        var source = f.Db.ManufacturingFormulaMaterials.Single();
        source.itemType = ItemType.MaterialFailure;
        source.LotNo = "SAVED-FAIL";
        var result = await f.Handler.Handle(new(f.Order.MfgProductionOrderId, f.Request(items: explicitItems ? [new() {
            SourceItemId = source.ManufacturingFormulaMaterialId, ItemId = f.Material.MaterialId,
            ItemType = ItemType.MaterialFailure, Quantity = 1, LotNo = source.LotNo }] : null)), default);
        Assert.False(result.Success);
        Assert.Empty(f.Store.Added);
        Assert.Equal(0, f.Store.Saves);
    }

    private static WarehouseShelfStock Stock(ManufacturingFormulaCreationTests.Fixture f, string lot)
        => new() { CompanyId = f.Order.CompanyId, Code = f.Material.ExternalId!, LotNo = lot,
            StockType = StockType.RawMaterial,
            WarehouseShelves = new() { CompanyId = f.Order.CompanyId, IsActive = true } };
}
