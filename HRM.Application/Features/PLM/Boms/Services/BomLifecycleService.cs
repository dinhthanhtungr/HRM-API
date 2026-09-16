using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Services;

internal sealed class BomLifecycleService
{
    private readonly IPLMWriteDbContext _dbContext;

    public BomLifecycleService(IPLMWriteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    internal async Task<string?> ValidateForReleaseAsync(
        BomVersion version,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (version.Status != BomVersionStatus.Draft)
        {
            return "Only Draft BOM versions can be released.";
        }

        if (version.Items.Count == 0)
        {
            return "At least one BOM item is required before release.";
        }

        if (version.BaseOutputQuantity <= 0 || string.IsNullOrWhiteSpace(version.OutputUnit))
        {
            return "BOM requires a positive BaseOutputQuantity and OutputUnit before release.";
        }

        if (version.BomDefinition.BomType == BomType.Engineering)
        {
            return await ValidateEngineeringCycleAsync(version, companyId, cancellationToken);
        }

        return await ValidateManufacturingBomAsync(version, companyId, cancellationToken);
    }

    private async Task<string?> ValidateManufacturingBomAsync(
        BomVersion version,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(version.OutputUnit, "kg", StringComparison.OrdinalIgnoreCase))
        {
            return "Manufacturing BOM output unit must be kg until unit conversion is supported.";
        }

        if (FormulaDrivenManufacturingBomMarker.IsFormulaDriven(version.ChangeReason))
        {
            // Formula only bootstraps the first M-BOM snapshot. Later operation is based on M-BOM/MfgFormula.
        }
        else if (version.SourceEngineeringBomVersionId is not { } sourceVersionId)
        {
            return "Manufacturing BOM requires a source Engineering BOM version or Formula-driven bootstrap marker.";
        }
        else
        {
            var validSource = await _dbContext.BomVersions
                .AsNoTracking()
                .AnyAsync(
                    x => x.BomVersionId == sourceVersionId &&
                         x.Status == BomVersionStatus.Released &&
                         x.BomDefinition.CompanyId == companyId &&
                         x.BomDefinition.ProductId == version.BomDefinition.ProductId &&
                         x.BomDefinition.BomType == BomType.Engineering,
                    cancellationToken);

            if (!validSource)
            {
                return "Source Engineering BOM must be Released and belong to the same Product and company.";
            }
        }

        if (version.ManufacturingStages.Count == 0)
        {
            return "Manufacturing BOM requires at least one active stage.";
        }

        if (version.Items.Any(x => x.ManufacturingBomStageId is null))
        {
            return "Every Manufacturing BOM item must be assigned to a stage.";
        }
        if (version.Items.Any(x => !string.Equals(x.Unit, "kg", StringComparison.OrdinalIgnoreCase)))
        {
            return "Every Manufacturing BOM item unit must be kg until unit conversion is supported.";
        }

        var stageIds = version.ManufacturingStages
            .Where(x => x.IsActive)
            .Select(x => x.ManufacturingBomStageId)
            .ToHashSet();
        var itemIds = version.Items.Select(x => x.BomVersionItemId).ToHashSet();
        if (version.Items.Any(x => !stageIds.Contains(x.ManufacturingBomStageId!.Value)))
        {
            return "Every Manufacturing BOM item must reference an active stage.";
        }

        var activeRules = version.LossRules.Where(x => x.IsActive).ToList();
        if (activeRules.GroupBy(x => x.SequenceNo).Any(x => x.Key <= 0 || x.Count() > 1))
        {
            return "Active loss rule SequenceNo must be positive and unique.";
        }
        var requestedLossTypeIds = activeRules.Select(x => x.ManufacturingLossTypeId).Distinct().ToArray();
        var activeLossTypeCount = await _dbContext.ManufacturingLossTypes
            .AsNoTracking()
            .CountAsync(
                x => x.CompanyId == companyId && x.IsActive && requestedLossTypeIds.Contains(x.ManufacturingLossTypeId),
                cancellationToken);
        if (activeLossTypeCount != requestedLossTypeIds.Length)
        {
            return "Every Manufacturing BOM loss rule must use an active loss type from the same company.";
        }

        foreach (var rule in activeRules)
        {
            var error = ValidateLossRuleForRelease(rule, stageIds, itemIds);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? ValidateLossRuleForRelease(
        ManufacturingBomLossRule rule,
        IReadOnlySet<Guid> stageIds,
        IReadOnlySet<Guid> itemIds)
    {
        if (!Enum.IsDefined(rule.CalculationMethod))
        {
            return "A Manufacturing BOM loss rule has an invalid CalculationMethod.";
        }
        if (rule.ManufacturingBomStageId.HasValue && !stageIds.Contains(rule.ManufacturingBomStageId.Value) ||
            rule.BomVersionItemId.HasValue && !itemIds.Contains(rule.BomVersionItemId.Value))
        {
            return "A Manufacturing BOM loss rule references an invalid stage or item.";
        }
        if (rule.IncludeInMaterialRequest && !rule.BomVersionItemId.HasValue)
        {
            return "A loss included in material requests must reference an item.";
        }

        return rule.CalculationMethod switch
        {
            LossCalculationMethod.PercentOfMaterial
                when !rule.BomVersionItemId.HasValue || rule.RatePercent is null or < 0 or >= 100
                => "PercentOfMaterial loss requires an item and RatePercent between 0 and 100.",
            LossCalculationMethod.PercentOfStageInput or LossCalculationMethod.PercentOfStageOutput
                when !rule.ManufacturingBomStageId.HasValue || rule.RatePercent is null or < 0 or >= 100
                => "Stage percentage loss requires a stage and RatePercent between 0 and 100.",
            LossCalculationMethod.FixedPerRun or LossCalculationMethod.FixedPerBatch
                when rule.FixedQuantityKg is null or < 0
                => "Fixed loss requires a non-negative FixedQuantityKg.",
            LossCalculationMethod.FixedPerEvent when rule.QuantityPerEventKg is null or < 0
                => "FixedPerEvent loss requires a non-negative QuantityPerEventKg.",
            LossCalculationMethod.ActualOnly when rule.IncludeInMaterialRequest
                => "ActualOnly loss cannot be included in material requests.",
            _ => null
        };
    }

    private async Task<string?> ValidateEngineeringCycleAsync(
        BomVersion version,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var parentProductId = version.BomDefinition.ProductId;
        var edges = await _dbContext.BomVersionItems
            .AsNoTracking()
            .Where(x =>
                x.ComponentProductId.HasValue &&
                x.BomVersion.Status == BomVersionStatus.Released &&
                x.BomVersion.BomDefinition.IsActive &&
                x.BomVersion.BomDefinition.CompanyId == companyId &&
                x.BomVersion.BomDefinition.BomType == BomType.Engineering)
            .Select(x => new
            {
                Parent = x.BomVersion.BomDefinition.ProductId,
                Child = x.ComponentProductId!.Value
            })
            .ToListAsync(cancellationToken);

        var graph = edges
            .GroupBy(x => x.Parent)
            .ToDictionary(x => x.Key, x => x.Select(edge => edge.Child).Distinct().ToArray());

        foreach (var componentId in version.Items
                     .Where(x => x.ItemType == ItemType.Product && x.ComponentProductId.HasValue)
                     .Select(x => x.ComponentProductId!.Value))
        {
            if (CanReach(componentId, parentProductId, graph))
            {
                return "Releasing this BOM would create a cyclic Product dependency.";
            }
        }

        return null;
    }

    private static bool CanReach(
        Guid start,
        Guid target,
        IReadOnlyDictionary<Guid, Guid[]> graph)
    {
        var visited = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(start);

        while (pending.TryPop(out var current))
        {
            if (current == target)
            {
                return true;
            }

            if (!visited.Add(current) || !graph.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                pending.Push(child);
            }
        }

        return false;
    }
}
