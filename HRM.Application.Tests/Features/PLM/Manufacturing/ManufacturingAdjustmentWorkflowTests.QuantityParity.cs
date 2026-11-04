using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Formulas;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed partial class ManufacturingAdjustmentWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextTrialBase_IsPreviousTrialResult_ForNormalAndAdditionalBatches(bool additional)
    {
        var f = new Fixture();
        var batchNo = additional ? 3 : 1;
        var request = new ManufacturingAdjustmentInput {
            Ranges = [
                f.Range(batchNo, additional ? AdjustmentType.SetQuantity : AdjustmentType.AddQuantity, additional ? 7500m : 50m),
                f.Range(batchNo, AdjustmentType.AddQuantity, -10m),
                f.Range(batchNo, AdjustmentType.AddQuantity, 0m)
            ]
        };
        var result = await f.Service.CalculateDraftAsync(f.Base.Order.MfgProductionOrderId, request, default);
        Assert.True(result.Success, result.Message);
        var batches = result.Data!.Adjustment.Batches.OrderBy(x => x.TrialNo).ToList();
        var expected = additional ? 7500m : 50050m;
        Assert.Equal(expected, Assert.Single(batches[1].Items).BaseQuantity);
        Assert.Equal(expected - 10m, Assert.Single(batches[2].Items).BaseQuantity);
        Assert.Equal(0m, Assert.Single(batches[2].Items).DeltaQuantity);
        Assert.Equal(expected - 10m, batches[2].FinalQuantityGram);
    }
    [Theory]
    [InlineData(50.125, 200)]
    [InlineData(-10.5, 500)]
    [InlineData(0, 1000)]
    public async Task QuantityParity_LegacyInputAndCanonicalSave_PreserveSignedGramAndTaken(decimal delta, decimal taken)
    {
        var f = new Fixture();
        var range = new ManufacturingAdjustmentRangeInput {
            BatchFrom = 1, BatchTo = 1, AdjustmentType = AdjustmentType.AddQuantity,
            TakenQuantityGram = taken,
            Items = [new() { ItemId = f.Base.Material.MaterialId, ItemType = ItemType.Material, QuantityGram = delta }]
        };
        var request = new ManufacturingAdjustmentInput { Ranges = [range] };
        var preview = await f.Service.CalculateDraftAsync(f.Base.Order.MfgProductionOrderId, request, default);
        Assert.True(preview.Success, preview.Message);
        var saved = await f.Service.SaveAsync(f.Base.Order.MfgProductionOrderId, null, request, default);
        Assert.True(saved.Success, saved.Message);
        var batch = Assert.Single(saved.Data!.Batches);
        var item = Assert.Single(batch.Items);
        // VAE RecalculateCell và CalculateFinalQuantityGram: gốc + delta, không trừ mẫu.
        Assert.Equal(50000m + delta, batch.FinalQuantityGram);
        Assert.Equal(Assert.Single(preview.Data!.Trials).FinalQuantityGram, batch.FinalQuantityGram);
        Assert.Equal(taken, batch.TakenQuantityGram);
        Assert.Equal(50000m, item.BaseQuantity);
        Assert.Equal(delta, item.DeltaQuantity);
        Assert.Equal(50000m + delta, item.AdjustedQuantity);
        Assert.Equal("g", item.Unit);
        Assert.True(f.Base.Store.Saves > 0);
    }
}
