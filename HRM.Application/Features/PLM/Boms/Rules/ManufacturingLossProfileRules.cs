using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Rules;

internal static class ManufacturingLossProfileRules
{
    internal static string? Validate(UpsertManufacturingLossProfileRequest request)
    {
        var error = BomRules.ValidateText(request.Name, 200, nameof(request.Name), true)
            ?? BomRules.ValidatePeriod(request.EffectiveFrom, request.EffectiveTo);
        if (error is not null)
        {
            return error;
        }

        if (request.Rules.Count > 500)
        {
            return "A loss profile cannot contain more than 500 rules.";
        }

        if (request.Rules.GroupBy(x => x.SequenceNo).Any(x => x.Key <= 0 || x.Count() > 1))
        {
            return "Every rule requires a unique positive SequenceNo.";
        }

        foreach (var rule in request.Rules)
        {
            error = ValidateRule(rule);
            if (error is not null)
            {
                return $"Rule {rule.SequenceNo}: {error}";
            }
        }

        return null;
    }

    private static string? ValidateRule(ManufacturingLossProfileRuleWriteDto rule)
    {
        if (rule.ManufacturingLossTypeId == Guid.Empty ||
            !Enum.IsDefined(rule.Scope) ||
            !Enum.IsDefined(rule.AllocationMethod) ||
            !Enum.IsDefined(rule.CalculationMethod))
        {
            return "loss type, scope, allocation method, or calculation method is invalid.";
        }

        var stageCode = BomRules.NormalizeOptionalText(rule.StageCode);
        var fromStageCode = BomRules.NormalizeOptionalText(rule.FromStageCode);
        var toStageCode = BomRules.NormalizeOptionalText(rule.ToStageCode);
        if (stageCode?.Length > 64 || fromStageCode?.Length > 64 || toStageCode?.Length > 64)
        {
            return "stage codes cannot exceed 64 characters.";
        }

        var targetError = rule.Scope switch
        {
            ManufacturingLossScope.Material when rule.MaterialId is null ||
                stageCode is not null || fromStageCode is not null || toStageCode is not null
                => "Material scope requires MaterialId and no stage target.",
            ManufacturingLossScope.Stage when rule.MaterialId is not null ||
                stageCode is null || fromStageCode is not null || toStageCode is not null
                => "Stage scope requires StageCode only.",
            ManufacturingLossScope.Transition when rule.MaterialId is not null ||
                stageCode is not null || fromStageCode is null || toStageCode is null ||
                string.Equals(fromStageCode, toStageCode, StringComparison.OrdinalIgnoreCase)
                => "Transition scope requires distinct FromStageCode and ToStageCode only.",
            ManufacturingLossScope.OverallBom when rule.MaterialId is not null ||
                stageCode is not null || fromStageCode is not null || toStageCode is not null
                => "OverallBom scope cannot have a material or stage target.",
            _ => null
        };
        if (targetError is not null)
        {
            return targetError;
        }

        if (rule.IncludeInMaterialRequest && rule.Scope != ManufacturingLossScope.Material)
        {
            return "IncludeInMaterialRequest is only valid for Material scope.";
        }

        return rule.CalculationMethod switch
        {
            LossCalculationMethod.PercentOfMaterial
                when rule.Scope != ManufacturingLossScope.Material || rule.RatePercent is null or < 0 or >= 100
                => "PercentOfMaterial requires Material scope and RatePercent between 0 and 100.",
            LossCalculationMethod.PercentOfStageInput or LossCalculationMethod.PercentOfStageOutput
                when rule.Scope != ManufacturingLossScope.Stage || rule.RatePercent is null or < 0 or >= 100
                => "stage percentage methods require Stage scope and RatePercent between 0 and 100.",
            LossCalculationMethod.FixedPerRun or LossCalculationMethod.FixedPerBatch
                when rule.FixedQuantityKg is null or < 0
                => "fixed methods require a non-negative FixedQuantityKg.",
            LossCalculationMethod.FixedPerEvent
                when rule.QuantityPerEventKg is null or < 0 || rule.DefaultEventCount is < 0
                => "FixedPerEvent requires non-negative QuantityPerEventKg and DefaultEventCount.",
            _ => null
        };
    }
}
