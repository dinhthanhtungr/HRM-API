using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Models;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Services;

internal sealed class ManufacturingBomStructureService
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly BomItemResolver _itemResolver;

    public ManufacturingBomStructureService(
        IPLMWriteDbContext dbContext,
        BomItemResolver itemResolver)
    {
        _dbContext = dbContext;
        _itemResolver = itemResolver;
    }

    internal async Task<ManufacturingBomStructureResolution> BuildAsync(
        Guid bomVersionId,
        Guid parentProductId,
        ReplaceManufacturingBomRequest request,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return ManufacturingBomStructureResolution.Fail(validationError);
        }

        var itemResolution = await _itemResolver.ResolveAsync(
            parentProductId,
            request.Items,
            companyId,
            cancellationToken);
        if (itemResolution.Error is not null)
        {
            return ManufacturingBomStructureResolution.Fail(itemResolution.Error);
        }

        var stages = request.Stages.Select(stage => new ManufacturingBomStage
        {
            ManufacturingBomStageId = Guid.CreateVersion7(),
            BomVersionId = bomVersionId,
            ExternalId = stage.Code.Trim(),
            Name = stage.Name.Trim(),
            SequenceNo = stage.SequenceNo,
            Description = BomRules.NormalizeOptionalText(stage.Description),
            IsActive = true
        }).ToList();
        var stagesByCode = stages.ToDictionary(x => x.ExternalId, StringComparer.OrdinalIgnoreCase);

        var items = itemResolution.Items.Select((item, index) => new BomVersionItem
        {
            BomVersionItemId = Guid.CreateVersion7(),
            BomVersionId = bomVersionId,
            LineNo = index + 1,
            ItemType = item.ItemType,
            MaterialId = item.ItemType == ItemType.Material ? item.ItemId : null,
            ComponentProductId = item.ItemType == ItemType.Product ? item.ItemId : null,
            CategoryId = item.CategoryId,
            ManufacturingBomStageId = stagesByCode[request.Items[index].ManufacturingStageCode!.Trim()].ManufacturingBomStageId,
            Quantity = item.Quantity,
            Unit = item.Unit,
            MaterialExternalIdSnapshot = item.Code,
            MaterialNameSnapshot = item.Name,
            Note = item.Note
        }).ToList();

        var requestedLossTypeIds = request.LossRules
            .Select(x => x.ManufacturingLossTypeId)
            .Distinct()
            .ToArray();
        var lossTypeIds = await _dbContext.ManufacturingLossTypes
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive && requestedLossTypeIds.Contains(x.ManufacturingLossTypeId))
            .Select(x => x.ManufacturingLossTypeId)
            .ToListAsync(cancellationToken);
        if (lossTypeIds.Count != requestedLossTypeIds.Length)
        {
            return ManufacturingBomStructureResolution.Fail("One or more loss types are inactive or outside your company.");
        }

        var rules = request.LossRules.Select(rule => new ManufacturingBomLossRule
        {
            ManufacturingBomLossRuleId = Guid.CreateVersion7(),
            BomVersionId = bomVersionId,
            ManufacturingLossTypeId = rule.ManufacturingLossTypeId,
            BomVersionItemId = rule.ItemLineNo.HasValue ? items[rule.ItemLineNo.Value - 1].BomVersionItemId : null,
            ManufacturingBomStageId = rule.StageCode is not null
                ? stagesByCode[rule.StageCode.Trim()].ManufacturingBomStageId
                : null,
            CalculationMethod = rule.CalculationMethod,
            RatePercent = rule.RatePercent,
            FixedQuantityKg = rule.FixedQuantityKg,
            QuantityPerEventKg = rule.QuantityPerEventKg,
            DefaultEventCount = rule.DefaultEventCount,
            SequenceNo = rule.SequenceNo,
            IsRecoverable = rule.IsRecoverable,
            IncludeInMaterialRequest = rule.IncludeInMaterialRequest,
            IsActive = true,
            Note = BomRules.NormalizeOptionalText(rule.Note)
        }).ToList();

        return ManufacturingBomStructureResolution.Ok(stages, items, rules);
    }

    private static string? ValidateRequest(ReplaceManufacturingBomRequest request)
    {
        var commonError = BomRules.ValidateText(request.OutputUnit, 32, nameof(request.OutputUnit), true)
            ?? BomRules.ValidatePeriod(request.EffectiveFrom, request.EffectiveTo);
        if (commonError is not null)
        {
            return commonError;
        }
        if (request.BaseOutputQuantity <= 0)
        {
            return "BaseOutputQuantity must be greater than zero.";
        }
        if (!string.Equals(request.OutputUnit.Trim(), "kg", StringComparison.OrdinalIgnoreCase))
        {
            return "Manufacturing BOM output unit must be kg until unit conversion is supported.";
        }
        if (request.Stages.Count == 0)
        {
            return "At least one manufacturing stage is required.";
        }
        if (request.Stages.Any(x => x.SequenceNo <= 0 ||
                                    string.IsNullOrWhiteSpace(x.Code) || x.Code.Trim().Length > 64 ||
                                    string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 200))
        {
            return "Every stage requires a positive SequenceNo, Code up to 64, and Name up to 200 characters.";
        }
        if (request.Stages.GroupBy(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1) ||
            request.Stages.GroupBy(x => x.SequenceNo).Any(x => x.Count() > 1))
        {
            return "Manufacturing stage Code and SequenceNo must be unique.";
        }

        var stageCodes = request.Stages.Select(x => x.Code.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (request.Items.Count == 0 || request.Items.Any(x =>
                string.IsNullOrWhiteSpace(x.ManufacturingStageCode) ||
                !stageCodes.Contains(x.ManufacturingStageCode.Trim())))
        {
            return "Every Manufacturing BOM item must reference a valid StageCode.";
        }
        if (request.Items.Any(x => !string.Equals(x.Unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase)))
        {
            return "Every Manufacturing BOM item unit must be kg until unit conversion is supported.";
        }
        if (request.LossRules.GroupBy(x => x.SequenceNo).Any(x => x.Key <= 0 || x.Count() > 1))
        {
            return "Loss rule SequenceNo must be positive and unique.";
        }

        foreach (var rule in request.LossRules)
        {
            var error = ValidateLossRule(rule, request.Items.Count, stageCodes);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? ValidateLossRule(
        ManufacturingBomLossRuleWriteDto rule,
        int itemCount,
        IReadOnlySet<string> stageCodes)
    {
        if (rule.ManufacturingLossTypeId == Guid.Empty)
        {
            return "ManufacturingLossTypeId is required for every loss rule.";
        }
        if (rule.ItemLineNo is <= 0 || rule.ItemLineNo > itemCount)
        {
            return "Loss rule ItemLineNo must reference an existing item.";
        }
        if (rule.StageCode is not null && !stageCodes.Contains(rule.StageCode.Trim()))
        {
            return "Loss rule StageCode must reference an existing stage.";
        }
        if (rule.IncludeInMaterialRequest && !rule.ItemLineNo.HasValue)
        {
            return "A loss included in material requests must reference ItemLineNo.";
        }

        return rule.CalculationMethod switch
        {
            LossCalculationMethod.PercentOfMaterial when !rule.ItemLineNo.HasValue || rule.RatePercent is null or < 0 or >= 100
                => "PercentOfMaterial requires ItemLineNo and RatePercent between 0 and 100.",
            LossCalculationMethod.PercentOfStageInput or LossCalculationMethod.PercentOfStageOutput
                when string.IsNullOrWhiteSpace(rule.StageCode) || rule.RatePercent is null or < 0 or >= 100
                => "Stage percentage loss requires StageCode and RatePercent between 0 and 100.",
            LossCalculationMethod.FixedPerRun or LossCalculationMethod.FixedPerBatch
                when rule.FixedQuantityKg is null or < 0
                => "Fixed loss requires a non-negative FixedQuantityKg.",
            LossCalculationMethod.FixedPerEvent when rule.QuantityPerEventKg is null or < 0
                => "FixedPerEvent requires a non-negative QuantityPerEventKg.",
            LossCalculationMethod.ActualOnly when rule.IncludeInMaterialRequest
                => "ActualOnly loss cannot be included in material requests.",
            _ => null
        };
    }
}
