using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Services;

internal static class ProductionLossCalculator
{
    internal static decimal CalculatePlannedQuantityKg(
        LossCalculationMethod method,
        decimal orderQuantityKg,
        int batchCount,
        decimal? scopedMaterialQuantityPerOutputKg,
        decimal? scopedStageQuantityPerOutputKg,
        decimal? ratePercent,
        decimal? fixedQuantityKg,
        decimal? quantityPerEventKg,
        int? eventCount)
    {
        var result = method switch
        {
            LossCalculationMethod.PercentOfMaterial =>
                orderQuantityKg * (scopedMaterialQuantityPerOutputKg ?? 0m) * (ratePercent ?? 0m) / 100m,
            LossCalculationMethod.PercentOfStageInput or LossCalculationMethod.PercentOfStageOutput =>
                orderQuantityKg * (scopedStageQuantityPerOutputKg ?? 0m) * (ratePercent ?? 0m) / 100m,
            LossCalculationMethod.FixedPerRun => fixedQuantityKg ?? 0m,
            LossCalculationMethod.FixedPerBatch => (fixedQuantityKg ?? 0m) * Math.Max(batchCount, 1),
            LossCalculationMethod.FixedPerEvent => (quantityPerEventKg ?? 0m) * Math.Max(eventCount ?? 0, 0),
            LossCalculationMethod.ActualOnly => 0m,
            _ => 0m
        };

        return decimal.Round(result, 3, MidpointRounding.AwayFromZero);
    }
}
