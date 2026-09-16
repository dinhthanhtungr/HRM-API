using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Application.Features.PLM.Boms.Mappers;

internal static class ProductionOrderLossMapper
{
    internal static ProductionOrderLossDto ToDto(MfgProductionOrderLoss entity) => new()
    {
        MfgProductionOrderLossId = entity.MfgProductionOrderLossId,
        SourceManufacturingBomLossRuleId = entity.SourceManufacturingBomLossRuleId,
        LossTypeCode = entity.LossTypeCodeSnapshot,
        LossTypeName = entity.LossTypeNameSnapshot,
        CalculationMethod = entity.CalculationMethodSnapshot,
        StageCode = entity.StageCodeSnapshot,
        MaterialCode = entity.MaterialCodeSnapshot,
        PlannedQuantityKg = entity.PlannedQuantityKg,
        ActualQuantityKg = entity.ActualQuantityKg,
        EventCount = entity.EventCount,
        RecoveredQuantityKg = entity.RecoveredQuantityKg,
        IncludeInMaterialRequest = entity.IncludeInMaterialRequestSnapshot,
        IsFinalized = entity.IsFinalized,
        Note = entity.Note
    };
}
