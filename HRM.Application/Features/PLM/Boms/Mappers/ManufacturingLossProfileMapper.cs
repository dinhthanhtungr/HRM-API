using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Entities.BomSchema;

namespace HRM.Application.Features.PLM.Boms.Mappers;

internal static class ManufacturingLossProfileMapper
{
    internal static ManufacturingLossProfileDto ToDto(ManufacturingLossProfile profile)
        => new()
        {
            ManufacturingLossProfileId = profile.ManufacturingLossProfileId,
            Code = profile.ExternalId,
            Name = profile.Name,
            Status = profile.Status,
            EffectiveFrom = profile.EffectiveFrom,
            EffectiveTo = profile.EffectiveTo,
            Description = profile.Description,
            CreatedDate = profile.CreatedDate,
            ReleasedDate = profile.ReleasedDate,
            UpdatedDate = profile.UpdatedDate,
            Rules = profile.Rules
                .OrderBy(x => x.SequenceNo)
                .Select(x => new ManufacturingLossProfileRuleDto
                {
                    ManufacturingLossProfileRuleId = x.ManufacturingLossProfileRuleId,
                    ManufacturingLossTypeId = x.ManufacturingLossTypeId,
                    LossTypeCode = x.LossType?.ExternalId ?? string.Empty,
                    LossTypeName = x.LossType?.Name ?? string.Empty,
                    MaterialId = x.MaterialId,
                    Scope = x.Scope,
                    StageCode = x.TargetStageCode,
                    FromStageCode = x.FromStageCode,
                    ToStageCode = x.ToStageCode,
                    AllocationMethod = x.AllocationMethod,
                    CalculationMethod = x.CalculationMethod,
                    RatePercent = x.RatePercent,
                    FixedQuantityKg = x.FixedQuantityKg,
                    QuantityPerEventKg = x.QuantityPerEventKg,
                    DefaultEventCount = x.DefaultEventCount,
                    SequenceNo = x.SequenceNo,
                    IsRecoverable = x.IsRecoverable,
                    IncludeInMaterialRequest = x.IncludeInMaterialRequest,
                    IsActive = x.IsActive,
                    Note = x.Note
                })
                .ToList()
        };
}
