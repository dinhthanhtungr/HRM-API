using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;

namespace HRM.Application.Tests.Features.PLM.Boms;

public sealed class ProductionLossCalculatorTests
{
    [Theory]
    [MemberData(nameof(Cases))]
    public void CalculatePlannedQuantityKg_AppliesConfiguredMethod(
        LossCalculationMethod method,
        decimal orderQuantityKg,
        int batchCount,
        decimal materialPerOutputKg,
        decimal stagePerOutputKg,
        decimal ratePercent,
        decimal fixedQuantityKg,
        decimal quantityPerEventKg,
        int eventCount,
        decimal expected)
    {
        var actual = ProductionLossCalculator.CalculatePlannedQuantityKg(
            method,
            orderQuantityKg,
            batchCount,
            materialPerOutputKg,
            stagePerOutputKg,
            ratePercent,
            fixedQuantityKg,
            quantityPerEventKg,
            eventCount);

        Assert.Equal(expected, actual);
    }

    public static IEnumerable<object[]> Cases =>
    [
        [LossCalculationMethod.PercentOfMaterial, 1000m, 1, 0.4m, 0m, 2.5m, 0m, 0m, 0, 10m],
        [LossCalculationMethod.PercentOfStageInput, 500m, 1, 0m, 1.2m, 3m, 0m, 0m, 0, 18m],
        [LossCalculationMethod.PercentOfStageOutput, 500m, 1, 0m, 0.8m, 3m, 0m, 0m, 0, 12m],
        [LossCalculationMethod.FixedPerRun, 1000m, 1, 0m, 0m, 0m, 2.75m, 0m, 0, 2.75m],
        [LossCalculationMethod.FixedPerBatch, 1000m, 4, 0m, 0m, 0m, 1.5m, 0m, 0, 6m],
        [LossCalculationMethod.FixedPerEvent, 1000m, 1, 0m, 0m, 0m, 0m, 0.3333m, 3, 1m],
        [LossCalculationMethod.ActualOnly, 1000m, 1, 1m, 1m, 99m, 99m, 99m, 99, 0m]
    ];

    [Fact]
    public void CalculatePlannedQuantityKg_RoundsHalfAwayFromZeroToThreeDecimals()
    {
        var actual = ProductionLossCalculator.CalculatePlannedQuantityKg(
            LossCalculationMethod.FixedPerEvent,
            1,
            1,
            null,
            null,
            null,
            null,
            0.0005m,
            1);

        Assert.Equal(0.001m, actual);
    }
}
