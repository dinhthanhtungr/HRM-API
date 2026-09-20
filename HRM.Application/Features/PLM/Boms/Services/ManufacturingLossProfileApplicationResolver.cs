using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Services;

internal static class ManufacturingLossProfileApplicationResolver
{
    internal static ManufacturingLossProfileResolution Resolve(
        BomVersion version,
        ManufacturingLossProfile profile,
        DateTime now)
    {
        if (version.Status != BomVersionStatus.Draft)
        {
            return ManufacturingLossProfileResolution.Fail("Only Draft Manufacturing BOM versions can receive a loss profile.");
        }
        if (profile.Status != ManufacturingLossProfileStatus.Released ||
            profile.EffectiveFrom.HasValue && profile.EffectiveFrom.Value > now ||
            profile.EffectiveTo.HasValue && profile.EffectiveTo.Value < now)
        {
            return ManufacturingLossProfileResolution.Fail("Only a Released and currently effective loss profile can be applied.");
        }

        var stagesByCode = version.ManufacturingStages
            .Where(x => x.IsActive)
            .ToDictionary(x => x.ExternalId, StringComparer.OrdinalIgnoreCase);
        var materialItems = version.Items
            .Where(x => x.MaterialId.HasValue)
            .GroupBy(x => x.MaterialId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var transitions = version.ManufacturingStageTransitions.ToList();
        var resolvedRules = new List<ManufacturingBomLossRule>();

        foreach (var source in profile.Rules.Where(x => x.IsActive).OrderBy(x => x.SequenceNo))
        {
            BomVersionItem? item = null;
            ManufacturingBomStage? stage = null;
            ManufacturingBomStageTransition? transition = null;

            if (source.Scope == ManufacturingLossScope.Material)
            {
                if (!source.MaterialId.HasValue ||
                    !materialItems.TryGetValue(source.MaterialId.Value, out var matches) || matches.Count != 1)
                {
                    return ManufacturingLossProfileResolution.Fail(
                        $"Rule {source.SequenceNo}: MaterialId must map to exactly one item in the target Manufacturing BOM.");
                }
                item = matches[0];
            }
            else if (source.Scope == ManufacturingLossScope.Stage)
            {
                if (string.IsNullOrWhiteSpace(source.TargetStageCode) ||
                    !stagesByCode.TryGetValue(source.TargetStageCode, out stage))
                {
                    return ManufacturingLossProfileResolution.Fail(
                        $"Rule {source.SequenceNo}: StageCode '{source.TargetStageCode}' does not map to an active target stage.");
                }
            }
            else if (source.Scope == ManufacturingLossScope.Transition)
            {
                if (string.IsNullOrWhiteSpace(source.FromStageCode) ||
                    string.IsNullOrWhiteSpace(source.ToStageCode) ||
                    !stagesByCode.TryGetValue(source.FromStageCode, out var fromStage) ||
                    !stagesByCode.TryGetValue(source.ToStageCode, out var toStage))
                {
                    return ManufacturingLossProfileResolution.Fail(
                        $"Rule {source.SequenceNo}: FromStageCode/ToStageCode does not map to active target stages.");
                }

                var matches = transitions.Where(x =>
                    x.FromManufacturingBomStageId == fromStage.ManufacturingBomStageId &&
                    x.ToManufacturingBomStageId == toStage.ManufacturingBomStageId).ToList();
                if (matches.Count != 1)
                {
                    return ManufacturingLossProfileResolution.Fail(
                        $"Rule {source.SequenceNo}: stage pair must map to exactly one target transition.");
                }
                transition = matches[0];
            }

            resolvedRules.Add(new ManufacturingBomLossRule
            {
                ManufacturingBomLossRuleId = Guid.CreateVersion7(),
                BomVersionId = version.BomVersionId,
                ManufacturingLossTypeId = source.ManufacturingLossTypeId,
                BomVersionItemId = item?.BomVersionItemId,
                ManufacturingBomStageId = stage?.ManufacturingBomStageId,
                ManufacturingBomStageTransitionId = transition?.ManufacturingBomStageTransitionId,
                Scope = source.Scope,
                AllocationMethod = source.AllocationMethod,
                CalculationMethod = source.CalculationMethod,
                RatePercent = source.RatePercent,
                FixedQuantityKg = source.FixedQuantityKg,
                QuantityPerEventKg = source.QuantityPerEventKg,
                DefaultEventCount = source.DefaultEventCount,
                SequenceNo = source.SequenceNo,
                IsRecoverable = source.IsRecoverable,
                IncludeInMaterialRequest = source.IncludeInMaterialRequest,
                IsActive = true,
                Note = source.Note,
                LossType = source.LossType,
                BomVersionItem = item,
                ManufacturingStage = stage,
                ManufacturingStageTransition = transition
            });
        }

        if (resolvedRules.Count == 0)
        {
            return ManufacturingLossProfileResolution.Fail("The loss profile has no active rules to apply.");
        }

        return ManufacturingLossProfileResolution.Ok(resolvedRules);
    }

    internal static ManufacturingLossProfileApplicationDto ToDto(
        Guid bomVersionId,
        ManufacturingLossProfile profile,
        IReadOnlyList<ManufacturingBomLossRule> rules,
        bool isPreview)
        => new()
        {
            BomVersionId = bomVersionId,
            ProfileId = profile.ManufacturingLossProfileId,
            ProfileCode = profile.ExternalId,
            IsPreview = isPreview,
            Rules = rules.OrderBy(x => x.SequenceNo).Select(x => new ManufacturingBomLossRuleDto
            {
                ManufacturingBomLossRuleId = x.ManufacturingBomLossRuleId,
                ManufacturingLossTypeId = x.ManufacturingLossTypeId,
                LossTypeCode = x.LossType.ExternalId,
                LossTypeName = x.LossType.Name,
                ItemLineNo = x.BomVersionItem?.LineNo,
                StageCode = x.ManufacturingStage?.ExternalId,
                TransitionCode = x.ManufacturingStageTransition?.ExternalId,
                Scope = x.Scope,
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
            }).ToList()
        };
}

internal sealed record ManufacturingLossProfileResolution(
    string? Error,
    IReadOnlyList<ManufacturingBomLossRule> Rules)
{
    internal static ManufacturingLossProfileResolution Fail(string error) => new(error, []);
    internal static ManufacturingLossProfileResolution Ok(IReadOnlyList<ManufacturingBomLossRule> rules) => new(null, rules);
}
