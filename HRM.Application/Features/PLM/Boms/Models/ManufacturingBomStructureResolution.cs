using HRM.Domain.Entities.BomSchema;

namespace HRM.Application.Features.PLM.Boms.Models;

internal sealed record ManufacturingBomStructureResolution(
    IReadOnlyList<ManufacturingBomStage> Stages,
    IReadOnlyList<BomVersionItem> Items,
    IReadOnlyList<ManufacturingBomLossRule> LossRules,
    string? Error)
{
    internal static ManufacturingBomStructureResolution Fail(string error)
        => new([], [], [], error);

    internal static ManufacturingBomStructureResolution Ok(
        IReadOnlyList<ManufacturingBomStage> stages,
        IReadOnlyList<BomVersionItem> items,
        IReadOnlyList<ManufacturingBomLossRule> rules)
        => new(stages, items, rules, null);
}
