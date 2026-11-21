using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.WareHouses;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed partial class ManufacturingAdjustmentWorkflowTests
{
    [Fact]
    public async Task InitializeVaWithGoodAndFailure_KeepsBothQualities()
    {
        var f = new Fixture();
        var formula = f.Base.Db.ManufacturingFormulas.Single();
        formula.ManufacturingFormulaMaterials.Add(new ManufacturingFormulaMaterial {
            ManufacturingFormulaMaterialId = Guid.NewGuid(), MaterialId = f.Base.Material.MaterialId,
            itemType = ItemType.MaterialFailure, Quantity = 0.2m, IsActive = true });
        var result = await f.Service.InitializeAsync(f.Base.Order.MfgProductionOrderId, null, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { ItemType.Material, ItemType.MaterialFailure },
            Assert.Single(result.Data!.Draft.Ranges!).Items.Select(x => x.ItemType).OrderBy(x => x));
        Assert.Equal(0, f.Base.Store.Saves);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SaveRange_MergesDuplicateRowsAndPreservesTotal(bool singleBatch, bool product)
    {
        var f = new Fixture();
        if (product) f.Base.Db.Products.First().CategoryId = f.Base.Material.CategoryId;
        var entity = f.Existing();
        var itemId = product ? f.Base.Db.Products.First().ProductId : f.Base.Material.MaterialId;
        var itemType = product ? ItemType.Product : ItemType.Material;
        var result = await f.Service.SaveRangeAsync(entity.ManufacturingFormulaAdjustmentId, null,
            new() { BatchFrom = 1, BatchTo = 1, TakenQuantityGram = 500,
                Items = [new() { ItemId = itemId, ItemType = itemType, QuantityGram = 100 },
                    new() { ItemId = itemId, ItemType = itemType, QuantityGram = 200 }] }, singleBatch, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(300m, Assert.Single(Assert.Single(result.Data!.Batches).Items).DeltaQuantity);
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(101, 1)]
    [InlineData(1000, 1)]
    public async Task Initialize_LargePlanKeepsFullPlanButSeedsBoundedEditableRange(int planned, int end)
    {
        var f = new Fixture();
        f.Base.Order.NumOfBatches = planned;
        var result = await f.Service.InitializeAsync(f.Base.Order.MfgProductionOrderId, null, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(planned, result.Data!.PlannedBatchCount);
        Assert.Equal(end, Assert.Single(result.Data.Draft.Ranges!).BatchTo);
        Assert.Equal(end, result.Data.Calculation.Adjustment.Batches.Count);
        Assert.Equal(0, f.Base.Store.Saves);
        if (planned > 100)
        {
            var saved = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
                new() { Ranges = [f.Range(planned, AdjustmentType.AddQuantity, 100)] }, default);
            Assert.True(saved.Success, saved.Message);
            Assert.Equal(planned, Assert.Single(saved.Data!.Batches).BatchNo);
            Assert.Equal(nameof(AdjustmentStatus.Approved), saved.Data.Status);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateRows_AreMergedOnBackendWithoutLosingQuantity(bool differentLots)
    {
        var f = new Fixture();
        var request = new ManufacturingAdjustmentInput { Ranges = [new() {
            BatchFrom = 1, BatchTo = 1, TakenQuantityGram = 500,
            Items = [new() { ItemId = f.Base.Material.MaterialId, ItemType = ItemType.Material,
                QuantityGram = 100 }, new() { ItemId = f.Base.Material.MaterialId, ItemType = ItemType.Material,
                QuantityGram = 200, LotNo = differentLots ? "ANOTHER-LOT" : null }]
        }] };
        // A nonexistent lot still fails its own scope/lot validation; duplicate rows alone do not.
        Assert.Equal(!differentLots, (await f.Service.CalculateDraftAsync(f.Base.Order.MfgProductionOrderId, request, default)).Success);
        var saved = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, request, default);
        Assert.Equal(!differentLots, saved.Success);
        if (saved.Success)
        {
            var line = Assert.Single(Assert.Single(saved.Data!.Batches).Items);
            Assert.Equal(300m, line.DeltaQuantity);
        }
        var document = new ManufacturingAdjustmentDocumentService(f.Base.Db, f.Base.User, f.Service, new PreviewRenderer());
        Assert.Equal(!differentLots, (await document.PreviewAsync(f.Base.Order.MfgProductionOrderId, request, default)).Success);
        if (differentLots)
        {
            Assert.Equal(0, f.Base.Store.Saves);
            Assert.Empty(f.Base.Store.Added);
        }
    }

    [Fact]
    public async Task InitializeStoredDuplicateRows_ReturnsMergedDraftWithoutUpdatingDatabase()
    {
        var f = new Fixture();
        var batch = new ManufacturingFormulaAdjustmentBatch { BatchNo = 1, TrialNo = 1,
            AdjustmentType = AdjustmentType.AddQuantity, BatchQuantity = 50000, TakenQuantityGram = 500,
            Status = nameof(AdjustmentStatus.Draft),
            Items = [new() { MaterialId = f.Base.Material.MaterialId, itemType = ItemType.Material,
                Unit = "g", BaseQuantity = 50000, AdjustedQuantity = 50100, DeltaQuantity = 100 },
                new() { MaterialId = f.Base.Material.MaterialId, itemType = ItemType.Material,
                Unit = "g", BaseQuantity = 50000, AdjustedQuantity = 50200, DeltaQuantity = 200 }] };
        var entity = f.Existing(batch);
        var opened = await f.Service.InitializeAsync(f.Base.Order.MfgProductionOrderId, entity.ManufacturingFormulaAdjustmentId, default);
        Assert.True(opened.Success, opened.Message);
        Assert.Single(Assert.Single(opened.Data!.Draft.Ranges!).Items);
        Assert.Single(Assert.Single(opened.Data.Calculation.Adjustment.Batches).Items);
        Assert.Equal(2, batch.Items.Count);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.Empty(f.Base.Store.Added);
    }

    [Theory]
    [InlineData(ItemType.Material)]
    [InlineData(ItemType.MaterialFailure)]
    [InlineData(ItemType.Product)]
    [InlineData(ItemType.ProductFailure)]
    public async Task SameLotWithGoodAndDefectiveStock_ResolvesSelectedQuality(ItemType selected)
    {
        var f = new Fixture();
        var product = selected is ItemType.Product or ItemType.ProductFailure;
        SeedBothQualities(f, product);
        var itemId = product ? f.Base.Db.Products.First().ProductId : f.Base.Material.MaterialId;
        var saved = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [new() {
                BatchFrom = 1, BatchTo = 1, TakenQuantityGram = 500,
                Items = [new() { ItemId = itemId, ItemType = selected,
                    QuantityGram = 100, LotNo = "SAME-LOT" }]
            }] }, default);
        Assert.True(saved.Success, saved.Message);
        Assert.Equal(selected, Assert.Single(Assert.Single(saved.Data!.Batches).Items).itemType);
        Assert.Equal(nameof(AdjustmentStatus.Approved), saved.Data.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameMaterialWithBothQualities_KeepsSeparateItemTypes(bool withLot)
    {
        var f = new Fixture();
        SeedBothQualities(f);
        var saved = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [new() {
                BatchFrom = 1, BatchTo = 1, TakenQuantityGram = 500,
                Items = new[] { ItemType.Material, ItemType.MaterialFailure }.Select(type =>
                    new ManufacturingAdjustmentItemInput { ItemId = f.Base.Material.MaterialId,
                        ItemType = type, QuantityGram = 100, LotNo = withLot ? "SAME-LOT" : null }).ToList()
            }] }, default);
        Assert.True(saved.Success, saved.Message);
        Assert.Equal(new[] { ItemType.Material, ItemType.MaterialFailure },
            Assert.Single(saved.Data!.Batches).Items.Select(x => x.itemType));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task EarlierTrialEdit_ValidatesEveryLaterTotalBeforeSaving(int final)
    {
        var f = new Fixture();
        var first = HistoricalBatch(f, 1, 50000, 100);
        var later = HistoricalBatch(f, 2, 50100, -50000 + final);
        var entity = f.Existing(first, later);
        var result = await f.Service.SaveRangeAsync(entity.ManufacturingFormulaAdjustmentId,
            first.ManufacturingFormulaAdjustmentBatchId, f.Range(1, AdjustmentType.AddQuantity, 0), false, default);
        Assert.Equal(final > 0, result.Success);
        if (final > 0)
        {
            Assert.Equal(final, result.Data!.Batches.Single(x => x.TrialNo == 2).FinalQuantityGram);
            Assert.Equal(nameof(AdjustmentStatus.Approved), result.Data.Status);
        }
        else
        {
            Assert.Equal(0, f.Base.Store.Saves);
            Assert.Equal(100m, Assert.Single(first.Items).DeltaQuantity);
            Assert.True(f.Base.Store.Transaction.RolledBack);
        }
    }

    [Fact]
    public async Task EarlierTrialDelete_CannotLeaveNegativeLaterTotal()
    {
        var f = new Fixture();
        var first = HistoricalBatch(f, 1, 50000, 100);
        var entity = f.Existing(first, HistoricalBatch(f, 2, 50100, -50050));
        var deleted = await f.Service.DeleteAsync(entity.ManufacturingFormulaAdjustmentId,
            first.ManufacturingFormulaAdjustmentBatchId, default);
        Assert.False(deleted.Success);
        Assert.True(first.IsActive);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    private static ManufacturingFormulaAdjustmentBatch HistoricalBatch(Fixture f, int trial, decimal before, decimal delta) => new() {
        ManufacturingFormulaAdjustmentBatchId = Guid.NewGuid(), BatchNo = 1, TrialNo = trial,
        AdjustmentType = AdjustmentType.AddQuantity, BatchQuantity = 50000, TakenQuantityGram = 500,
        FinalQuantityGram = before + delta,
        Items = [new() { MaterialId = f.Base.Material.MaterialId, itemType = ItemType.Material,
            BaseQuantity = before, AdjustedQuantity = before + delta, DeltaQuantity = delta, Unit = "g" }]
    };

    private static void SeedBothQualities(Fixture f, bool product = false)
    {
        if (product)
        {
            var source = f.Base.Db.Products.First();
            source.CategoryId = f.Base.Material.CategoryId;
            source.ColourCode = "TP-QA-COLOR";
        }
        WarehouseShelfStock Stock(StockType quality) => new() {
            CompanyId = f.Base.Order.CompanyId, Code = product ? f.Base.Db.Products.First().ColourCode! : f.Base.Material.ExternalId!, LotNo = "SAME-LOT",
            StockType = quality, WarehouseShelves = new() { CompanyId = f.Base.Order.CompanyId, IsActive = true }
        };
        f.Base.Store.Seed(nameof(f.Base.Db.WarehouseShelfStocks),
            Stock(product ? StockType.FinishedGood : StockType.RawMaterial),
            Stock(product ? StockType.DefectiveFinishedGood : StockType.DefectiveRawMaterial));
    }
}
