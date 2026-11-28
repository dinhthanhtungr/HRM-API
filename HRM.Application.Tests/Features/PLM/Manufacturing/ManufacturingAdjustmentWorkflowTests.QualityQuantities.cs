using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed partial class ManufacturingAdjustmentWorkflowTests
{
    [PostgresGroupStorageFact]
    public async Task QualityQuantities_PostgresRoundTripPreservesBothQualitiesAndLeavesOldRowsUntouched()
    {
        var f = new Fixture();
        var (itemId, good, bad) = QualityFormula(f, true);
        var saved = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [QualityRange(itemId, good, bad, 1000m, -500m)] }, default);
        Assert.True(saved.Success, saved.Message);
        var postgres = new IsolatedGroupStoragePostgres();
        await postgres.InitializeAsync();
        try
        {
            await using var connection = await postgres.OpenAsync();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            foreach (var type in new[] { typeof(ManufacturingFormulaAdjustment), typeof(ManufacturingFormulaAdjustmentBatch), typeof(ManufacturingFormulaAdjustmentItem) })
            {
                var entity = db.Model.FindEntityType(type)!;
                await HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests.CreateTable(connection, entity);
                // The shared test table helper creates columns without defaults. Restore
                // trusted model defaults only in this fresh loopback test cluster.
                foreach (var property in entity.GetProperties())
                {
                    var sql = property.GetDefaultValueSql();
                    if (sql is null && property.GetDefaultValue() is { } value)
                        sql = property.GetRelationalTypeMapping().GenerateSqlLiteral(value);
                    if (sql is null) continue;
                    await using var defaults = connection.CreateCommand();
                    defaults.CommandText = $"ALTER TABLE \"{entity.GetSchema()}\".\"{entity.GetTableName()}\" ALTER COLUMN \"{property.GetColumnName()}\" SET DEFAULT {sql}";
                    await defaults.ExecuteNonQueryAsync();
                }
            }
            var legacy = Copy(saved.Data!);
            legacy.Status = nameof(AdjustmentStatus.Draft);
            var legacyAmounts = new[] { 777m, 888m };
            foreach (var pair in legacy.Batches.Single().Items.Zip(legacyAmounts))
            {
                pair.First.BaseQuantity = pair.Second;
                pair.First.AdjustedQuantity = pair.Second;
                pair.First.DeltaQuantity = 0m;
            }
            db.ManufacturingFormulaAdjustments.Add(legacy);
            await db.SaveChangesAsync();
            var current = Copy(saved.Data!);
            db.ManufacturingFormulaAdjustments.Add(current);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var reopened = await db.ManufacturingFormulaAdjustments.AsNoTracking()
                .Include(x => x.Batches).ThenInclude(x => x.Items)
                .SingleAsync(x => x.ManufacturingFormulaAdjustmentId == current.ManufacturingFormulaAdjustmentId);
            Assert.Equal(nameof(AdjustmentStatus.Approved), reopened.Status);
            var batch = Assert.Single(reopened.Batches);
            var rows = batch.Items.OrderBy(x => x.itemType).ToList();
            Assert.Equal(new[] { good, bad }, rows.Select(x => x.itemType));
            Assert.Equal(new decimal?[] { 30000m, 20000m }, rows.Select(x => x.BaseQuantity));
            Assert.Equal(new decimal?[] { 31000m, 19500m }, rows.Select(x => x.AdjustedQuantity));
            Assert.Equal(50500m, Assert.Single(MfgAdjustmentQuantityCalculator.CalculateTrialTotals(ManufacturingAdjustmentBatchRules.ToRows(batch))).FinalQuantityGram);
            var unchanged = await db.ManufacturingFormulaAdjustmentItems.AsNoTracking()
                .Where(x => x.ManufacturingFormulaAdjustmentBatchId == legacy.Batches.Single().ManufacturingFormulaAdjustmentBatchId)
                .OrderBy(x => x.itemType).Select(x => x.BaseQuantity).ToListAsync();
            Assert.Equal(legacyAmounts.Select(x => (decimal?)x), unchanged);
            Assert.Equal(nameof(AdjustmentStatus.Draft), (await db.ManufacturingFormulaAdjustments.AsNoTracking()
                .SingleAsync(x => x.ManufacturingFormulaAdjustmentId == legacy.ManufacturingFormulaAdjustmentId)).Status);
        }
        finally { await postgres.DisposeAsync(); }

        ManufacturingFormulaAdjustment Copy(GetManufacturingFormulaAdjustment source) => new() {
            ManufacturingFormulaAdjustmentId = Guid.NewGuid(), MfgProductionOrderId = source.MfgProductionOrderId,
            CompanyId = f.Base.Order.CompanyId, Status = source.Status, CreatedBy = f.Base.User.EmployeeId!.Value,
            CreatedDate = ManufacturingFormulaCreationTests.Fixture.Now,
            Batches = source.Batches.Select(batch => new ManufacturingFormulaAdjustmentBatch {
                ManufacturingFormulaAdjustmentBatchId = Guid.NewGuid(), BatchNo = batch.BatchNo, TrialNo = batch.TrialNo,
                Status = batch.Status, AdjustmentType = batch.AdjustmentType, BatchQuantity = batch.BatchQuantity,
                FinalQuantityGram = batch.FinalQuantityGram, TakenQuantityGram = batch.TakenQuantityGram,
                CreatedBy = f.Base.User.EmployeeId.Value, CreatedDate = ManufacturingFormulaCreationTests.Fixture.Now,
                Items = batch.Items.Select(item => new ManufacturingFormulaAdjustmentItem {
                    ManufacturingFormulaAdjustmentItemId = Guid.NewGuid(), MaterialId = item.MaterialId, ProductId = item.ProductId,
                    CategoryId = item.CategoryId, itemType = item.itemType, BaseQuantity = item.BaseQuantity,
                    AdjustedQuantity = item.AdjustedQuantity, DeltaQuantity = item.DeltaQuantity, Unit = item.Unit,
                    LineNo = item.LineNo
                }).ToList()
            }).ToList()
        };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QualityQuantities_InitializeSaveReopenAndNextTrialKeepSeparateBases(bool product)
    {
        var f = new Fixture();
        var (itemId, good, bad) = QualityFormula(f, product);
        var initialized = await f.Service.InitializeAsync(f.Base.Order.MfgProductionOrderId, null, default);
        Assert.True(initialized.Success, initialized.Message);
        Assert.Equal(new decimal?[] { 30000m, 20000m },
            initialized.Data!.Calculation.Adjustment.Batches.First().Items.Select(x => x.BaseQuantity));
        var first = QualityRange(itemId, good, bad, 1000m, -500m);
        var saved = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Status = AdjustmentStatus.Draft, Ranges = [first] }, default);
        Assert.True(saved.Success, saved.Message);
        Assert.Equal(nameof(AdjustmentStatus.Approved), saved.Data!.Status);
        var firstBatch = Assert.Single(saved.Data.Batches);
        Assert.Equal(new decimal?[] { 30000m, 20000m }, firstBatch.Items.Select(x => x.BaseQuantity));
        Assert.Equal(new decimal?[] { 31000m, 19500m }, firstBatch.Items.Select(x => x.AdjustedQuantity));
        Assert.Equal(50500m, firstBatch.FinalQuantityGram);
        var entity = Assert.Single(f.Base.Store.Added.OfType<ManufacturingFormulaAdjustment>());
        f.Base.Store.Seed(nameof(f.Base.Db.ManufacturingFormulaAdjustments), entity);
        var saves = f.Base.Store.Saves;
        var opened = await f.Service.InitializeAsync(f.Base.Order.MfgProductionOrderId,
            saved.Data.ManufacturingFormulaAdjustmentId, default);
        Assert.True(opened.Success, opened.Message);
        Assert.Equal(saves, f.Base.Store.Saves);
        var itemTotals = opened.Data!.Calculation.Trials.Single().Items.OrderBy(x => x.ItemType).ToList();
        Assert.Equal(new[] { good, bad }, itemTotals.Select(x => x.ItemType));
        Assert.Equal(new[] { 30000m, 20000m }, itemTotals.Select(x => x.OriginalBaseQuantityGram));
        Assert.Equal(new[] { 31000m, 19500m }, itemTotals.Select(x => x.ActualQuantityGram));
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(
            itemTotals.Last(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        Assert.Equal(bad.ToString(), json.RootElement.GetProperty("itemType").GetString());

        var second = await f.Service.SaveRangeAsync(entity.ManufacturingFormulaAdjustmentId,
            null, QualityRange(itemId, good, bad, 200m, 400m), false, default);
        Assert.True(second.Success, second.Message);
        var secondBatch = second.Data!.Batches.Single(x => x.TrialNo == 2);
        Assert.Equal(new decimal?[] { 31000m, 19500m }, secondBatch.Items.Select(x => x.BaseQuantity));
        Assert.Equal(new decimal?[] { 31200m, 19900m }, secondBatch.Items.Select(x => x.AdjustedQuantity));
        Assert.Equal(51100m, secondBatch.FinalQuantityGram);
        Assert.All(second.Data.Batches, batch => Assert.Equal(nameof(AdjustmentStatus.Approved), batch.Status));
        var renderer = new PreviewRenderer();
        var document = new ManufacturingAdjustmentDocumentService(f.Base.Db, f.Base.User, f.Service, renderer);
        var printed = await document.PrintAsync(entity.ManufacturingFormulaAdjustmentId, null, default);
        Assert.True(printed.Success, printed.Message);
        Assert.Equal(new decimal?[] { 31000m, 19500m },
            renderer.Document!.Batches.Single(x => x.TrialNo == 2).Items.Select(x => x.BaseQuantity));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QualityQuantities_WeighingAndSubsequentDeltasDoNotDoubleEitherQuality(bool product)
    {
        var f = new Fixture();
        var (itemId, good, bad) = QualityFormula(f, product);
        var initial = QualityRange(itemId, good, bad, 30000m, 20000m, AdjustmentType.SetQuantity);
        var delta = QualityRange(itemId, good, bad, 1000m, -500m);
        var request = new ManufacturingAdjustmentInput { Ranges = [initial, delta] };
        var preview = await f.Service.CalculateDraftAsync(f.Base.Order.MfgProductionOrderId, request, default);
        Assert.True(preview.Success, preview.Message);
        var saved = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, request, default);
        Assert.True(saved.Success, saved.Message);
        Assert.Equal(new decimal?[] { 50000m, 50500m }, saved.Data!.Batches.OrderBy(x => x.TrialNo).Select(x => x.FinalQuantityGram));
        var secondItems = saved.Data.Batches.Single(x => x.TrialNo == 2).Items;
        Assert.Equal(new decimal?[] { 30000m, 20000m }, secondItems.Select(x => x.BaseQuantity));
        Assert.Equal(new decimal?[] { 31000m, 19500m }, secondItems.Select(x => x.AdjustedQuantity));
        Assert.Equal(preview.Data!.Adjustment.Batches.Select(x => x.FinalQuantityGram), saved.Data.Batches.Select(x => x.FinalQuantityGram));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QualityQuantities_ReadDoesNotRewriteHistoricalAmountsOrStatus(bool product)
    {
        var f = new Fixture();
        var (itemId, good, bad) = QualityFormula(f, product);
        var original = new[] { 12345m, 23456m };
        var batch = new ManufacturingFormulaAdjustmentBatch {
            BatchNo = 1, TrialNo = 1, AdjustmentType = AdjustmentType.AddQuantity,
            TakenQuantityGram = 500,
            Items = new[] { good, bad }.Select((type, index) => new ManufacturingFormulaAdjustmentItem {
                MaterialId = product ? null : itemId, ProductId = product ? itemId : null,
                itemType = type, BaseQuantity = original[index], AdjustedQuantity = original[index], DeltaQuantity = 0m, Unit = "g"
            }).ToList()
        };
        var stored = f.Existing(batch);
        var read = await f.Service.GetAsync(stored.ManufacturingFormulaAdjustmentId, default);
        Assert.True(read.Success, read.Message);
        Assert.Equal(35801m, Assert.Single(read.Data!.Batches).FinalQuantityGram);
        Assert.Equal(original.Select(x => (decimal?)x), Assert.Single(read.Data.Batches).Items.Select(x => x.BaseQuantity));
        Assert.Equal(original.Select(x => (decimal?)x), batch.Items.Select(x => x.BaseQuantity));
        Assert.Equal(nameof(AdjustmentStatus.Draft), stored.Status);
        Assert.Equal(0, f.Base.Store.Saves);
    }

    private static (Guid Id, ItemType Good, ItemType Bad) QualityFormula(Fixture f, bool product)
    {
        var id = f.Base.Material.MaterialId;
        if (product)
        {
            var source = f.Base.Db.Products.First();
            source.CategoryId = f.Base.Material.CategoryId;
            source.ColourCode = "TP-QUALITY";
            id = source.ProductId;
        }
        var good = product ? ItemType.Product : ItemType.Material;
        var bad = product ? ItemType.ProductFailure : ItemType.MaterialFailure;
        var formula = f.Base.Db.ManufacturingFormulas.Single();
        formula.ManufacturingFormulaMaterials.Clear();
        foreach (var pair in new[] { (good, 0.6m), (bad, 0.4m) })
            formula.ManufacturingFormulaMaterials.Add(new ManufacturingFormulaMaterial {
                ManufacturingFormulaMaterialId = Guid.NewGuid(), ManufacturingFormulaId = formula.ManufacturingFormulaId,
                MaterialId = product ? null : id, ProductId = product ? id : null,
                CategoryId = f.Base.Material.CategoryId, itemType = pair.Item1, Quantity = pair.Item2,
                IsActive = true, Unit = "kg"
            });
        return (id, good, bad);
    }

    private static ManufacturingAdjustmentRangeInput QualityRange(Guid id, ItemType good, ItemType bad,
        decimal goodGram, decimal badGram, AdjustmentType mode = AdjustmentType.AddQuantity) => new() {
        BatchFrom = 1, BatchTo = 1, AdjustmentType = mode, TakenQuantityGram = 500,
        Items = [new() { ItemId = id, ItemType = good, QuantityGram = goodGram },
            new() { ItemId = id, ItemType = bad, QuantityGram = badGram }]
    };
}
