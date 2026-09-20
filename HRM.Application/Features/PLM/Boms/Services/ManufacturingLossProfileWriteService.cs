using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Services;

internal sealed class ManufacturingLossProfileWriteService
{
    private readonly IPLMWriteDbContext _dbContext;

    public ManufacturingLossProfileWriteService(IPLMWriteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    internal async Task<(string? Error, List<ManufacturingLossProfileRule> Rules)> ValidateAndBuildRulesAsync(
        Guid profileId,
        Guid companyId,
        UpsertManufacturingLossProfileRequest request,
        CancellationToken cancellationToken)
    {
        var error = ManufacturingLossProfileRules.Validate(request);
        if (error is not null)
        {
            return (error, []);
        }

        var lossTypeIds = request.Rules.Select(x => x.ManufacturingLossTypeId).Distinct().ToList();
        var lossTypes = await _dbContext.ManufacturingLossTypes
            .Where(x => lossTypeIds.Contains(x.ManufacturingLossTypeId) &&
                        x.CompanyId == companyId && x.IsActive)
            .ToDictionaryAsync(x => x.ManufacturingLossTypeId, cancellationToken);
        if (lossTypes.Count != lossTypeIds.Count)
        {
            return ("One or more loss types are inactive or outside your company.", []);
        }

        var materialIds = request.Rules
            .Where(x => x.MaterialId.HasValue)
            .Select(x => x.MaterialId!.Value)
            .Distinct()
            .ToList();
        var validMaterialCount = await _dbContext.Materials.AsNoTracking()
            .CountAsync(x => materialIds.Contains(x.MaterialId) &&
                             x.CompanyId == companyId && x.IsActive == true,
                cancellationToken);
        if (validMaterialCount != materialIds.Count)
        {
            return ("One or more materials are inactive or outside your company.", []);
        }

        return (null, request.Rules.Select(x => new ManufacturingLossProfileRule
        {
            ManufacturingLossProfileRuleId = Guid.CreateVersion7(),
            ManufacturingLossProfileId = profileId,
            ManufacturingLossTypeId = x.ManufacturingLossTypeId,
            LossType = lossTypes[x.ManufacturingLossTypeId],
            MaterialId = x.MaterialId,
            Scope = x.Scope,
            TargetStageCode = BomRules.NormalizeOptionalText(x.StageCode),
            FromStageCode = BomRules.NormalizeOptionalText(x.FromStageCode),
            ToStageCode = BomRules.NormalizeOptionalText(x.ToStageCode),
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
            Note = BomRules.NormalizeOptionalText(x.Note)
        }).ToList());
    }
}
