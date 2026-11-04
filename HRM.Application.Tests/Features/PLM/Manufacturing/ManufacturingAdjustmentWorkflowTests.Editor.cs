using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Domain.Enums.Manufacturings;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed partial class ManufacturingAdjustmentWorkflowTests
{
    [Fact]
    public async Task Initialize_SeedsFullPlanAndServerFormulaAmountsWithoutWriting()
    {
        var f = new Fixture();
        var result = await f.Service.InitializeAsync(f.Base.Order.MfgProductionOrderId, null, default);
        Assert.True(result.Success, result.Message);
        var editor = result.Data!;
        Assert.Null(editor.SourceAdjustmentId);
        Assert.Equal(2, editor.PlannedBatchCount);
        Assert.Equal(50000m, editor.PlannedBatchQuantityGram);
        Assert.Equal(2, Assert.Single(editor.Draft.Ranges!).BatchTo);
        Assert.All(editor.Calculation.Adjustment.Batches, b =>
        {
            Assert.Equal(50000m, b.FinalQuantityGram);
            Assert.Equal(50000m, Assert.Single(b.Items).BaseQuantity);
            Assert.Equal(Guid.Empty, b.ManufacturingFormulaAdjustmentBatchId);
            Assert.Equal(Guid.Empty, Assert.Single(b.Items).ManufacturingFormulaAdjustmentItemId);
        });
        Assert.False(editor.Calculation.IsStored);
        Assert.Single(editor.Calculation.Groups.BatchGroups);
        Assert.Empty(f.Base.Store.Added);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.Empty(f.Base.Notifications.Requests);
    }

    [Fact]
    public async Task Calculate_MultipleWeighingsAndSignedDelta_MatchesSaveAndTrialItemTotals()
    {
        var f = new Fixture();
        var request = new ManufacturingAdjustmentInput
        {
            Ranges = [f.Range(3, AdjustmentType.SetQuantity, 10000),
                f.Range(3, AdjustmentType.SetQuantity, 5000), f.Range(3, AdjustmentType.AddQuantity, -100)]
        };
        var preview = await f.Service.CalculateDraftAsync(f.Base.Order.MfgProductionOrderId, request, default);
        Assert.True(preview.Success, preview.Message);
        Assert.Equal(new decimal[] { 10000, 15000, 14900 }, preview.Data!.Trials.Select(x => x.FinalQuantityGram));
        Assert.Equal(14900m, Assert.Single(preview.Data.Trials.Last().Items).ActualQuantityGram);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.Empty(f.Base.Notifications.Requests);
        var saved = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, request, default);
        Assert.True(saved.Success, saved.Message);
        Assert.Equal(preview.Data.Adjustment.Batches.Select(x => x.FinalQuantityGram), saved.Data!.Batches.Select(x => x.FinalQuantityGram));
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("missing-employee")]
    [InlineData("missing-material-permission")]
    [InlineData("missing-manage-permission")]
    [InlineData("foreign-order")]
    [InlineData("foreign-adjustment")]
    [InlineData("wrong-order-adjustment")]
    [InlineData("unrelated-formula")]
    public async Task Editor_RejectsUnauthorizedOrUnrelatedContext(string reason)
    {
        var f = new Fixture();
        Guid? id = null;
        if (reason == "anonymous") f.Base.User.IsAuthenticated = false;
        if (reason == "missing-employee") f.Base.User.EmployeeId = null;
        if (reason is "missing-material-permission" or "missing-manage-permission")
        {
            f.Base.User.HasExplicitPermissionSet = true;
            f.Base.User.Permissions = reason == "missing-material-permission"
                ? [ApplicationPermissions.PLM.ManageManufacturingAdjustments]
                : [ApplicationPermissions.PLM.ViewProductionOrders, ApplicationPermissions.PLM.ViewFormulaMaterials];
        }
        if (reason == "foreign-order") f.Base.Order.CompanyId = Guid.NewGuid();
        if (reason is "foreign-adjustment" or "wrong-order-adjustment")
        {
            var entity = f.Existing(); id = entity.ManufacturingFormulaAdjustmentId;
            if (reason == "foreign-adjustment") entity.CompanyId = Guid.NewGuid();
            else entity.MfgProductionOrderId = Guid.NewGuid();
        }
        var input = new ManufacturingAdjustmentInput
        {
            ManufacturingFormulaId = reason == "unrelated-formula" ? Guid.NewGuid() : null,
            Ranges = [f.Range(1, AdjustmentType.AddQuantity, 1)]
        };
        Assert.False((await f.Service.CalculateDraftAsync(f.Base.Order.MfgProductionOrderId, input, default, id)).Success);
        if (reason != "unrelated-formula")
            Assert.False((await f.Service.InitializeAsync(f.Base.Order.MfgProductionOrderId, id, default)).Success);
        Assert.Equal(0, f.Base.Store.Saves);
        Assert.Empty(f.Base.Notifications.Requests);
    }

    [Fact]
    public async Task InitializeStoredDraft_PreservesHistoricalBaseWhenPlanChanges()
    {
        var f = new Fixture();
        var saved = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null,
            new() { Ranges = [f.Range(1, AdjustmentType.AddQuantity, 250)] }, default);
        Assert.True(saved.Success, saved.Message);
        var stored = Assert.Single(f.Base.Store.Added.OfType<HRM.Domain.Entities.ManufacturingSchema.ManufacturingFormulaAdjustment>());
        f.Base.Store.Seed(nameof(f.Base.Db.ManufacturingFormulaAdjustments), stored);
        f.Base.Order.TotalQuantity = 200;
        var result = await f.Service.InitializeAsync(f.Base.Order.MfgProductionOrderId, stored.ManufacturingFormulaAdjustmentId, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(50250m, Assert.Single(result.Data!.Calculation.Trials).FinalQuantityGram);
        Assert.Equal(250m, Assert.Single(Assert.Single(result.Data.Draft.Ranges!).Items).QuantityGram);
        var renderer = new PreviewRenderer();
        var document = new HRM.Application.Features.PLM.Manufacturing.Services.Adjustments.ManufacturingAdjustmentDocumentService(
            f.Base.Db, f.Base.User, f.Service, renderer);
        var pdf = await document.PreviewAsync(f.Base.Order.MfgProductionOrderId, result.Data.Draft, default, stored.ManufacturingFormulaAdjustmentId);
        Assert.False(pdf.Success);
        Assert.Contains("Chưa có lần chỉnh chưa lưu", pdf.Message);
        Assert.Null(renderer.Document);

        var withNewTrial = new ManufacturingAdjustmentInput
        {
            ManufacturingFormulaId = result.Data.Draft.ManufacturingFormulaId,
            Ranges = [.. result.Data.Draft.Ranges!, f.Range(1, AdjustmentType.AddQuantity, 100)]
        };
        pdf = await document.PreviewAsync(f.Base.Order.MfgProductionOrderId, withNewTrial, default, stored.ManufacturingFormulaAdjustmentId);
        Assert.True(pdf.Success, pdf.Message);
        var trial = Assert.Single(renderer.Document!.Batches);
        Assert.Equal(2, trial.TrialNo);
        Assert.Equal(50350m, trial.FinalQuantityGram);
        Assert.Equal(50250m, Assert.Single(trial.Items).BaseQuantity);

        var printed = await document.PrintAsync(stored.ManufacturingFormulaAdjustmentId, null, default);
        Assert.True(printed.Success, printed.Message);
        Assert.Equal(1, Assert.Single(renderer.Document!.Batches).TrialNo);
        Assert.Equal(50250m, Assert.Single(renderer.Document.Batches).FinalQuantityGram);
    }

    [Fact]
    public async Task InitializeLegacyKgRows_ConvertsMissingDeltaToGramInput()
    {
        var f = new Fixture();
        var stored = f.Existing(new HRM.Domain.Entities.ManufacturingSchema.ManufacturingFormulaAdjustmentBatch()
        {
            BatchNo = 1, TrialNo = 1, Status = "Draft", AdjustmentType = AdjustmentType.AddQuantity, BatchQuantity = 50000m,
            Items = [new()
            {
                MaterialId = f.Base.Material.MaterialId, itemType = HRM.Domain.Enums.Formulas.ItemType.Material,
                BaseQuantity = 50m, AdjustedQuantity = 50.25m, DeltaQuantity = null, Unit = "kg"
            }]
        });
        stored.Status = "Draft";
        var result = await f.Service.InitializeAsync(f.Base.Order.MfgProductionOrderId, stored.ManufacturingFormulaAdjustmentId, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(250m, Assert.Single(Assert.Single(result.Data!.Draft.Ranges!).Items).QuantityGram);
        Assert.Equal(50250m, Assert.Single(result.Data.Calculation.Trials).FinalQuantityGram);
    }

    [Fact]
    public async Task Calculate_RejectsNullItemsAndOversizedDraftWithoutWrites()
    {
        var f = new Fixture();
        var invalid = new ManufacturingAdjustmentInput { Ranges = [new() { BatchFrom = 1, BatchTo = 1, Items = null! }] };
        Assert.False((await f.Service.CalculateDraftAsync(f.Base.Order.MfgProductionOrderId, invalid, default)).Success);
        var oversized = new ManufacturingAdjustmentInput { Ranges = [f.Range(1, AdjustmentType.AddQuantity, 0, 100), f.Range(101, AdjustmentType.SetQuantity, 1)] };
        Assert.False((await f.Service.CalculateDraftAsync(f.Base.Order.MfgProductionOrderId, oversized, default)).Success);
        Assert.Equal(0, f.Base.Store.Saves);
    }
}
