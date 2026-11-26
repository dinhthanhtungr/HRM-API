namespace HRM.Application.Features.ExcelSpecial.Queries.ExportMaterialUsage;

internal sealed record MaterialUsageCandidate(Guid ManufacturingFormulaId, string ExternalId,
    DateTime CreatedDate, string ColourCode, string Name);

internal static class MaterialUsageSelection
{
    internal static IReadOnlyList<MaterialUsageCandidate> Latest(IEnumerable<MaterialUsageCandidate> candidates) =>
        candidates.GroupBy(x => x.ColourCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.ExternalId, StringComparer.Ordinal)
                .ThenByDescending(x => x.ManufacturingFormulaId).First())
            .OrderBy(x => x.ColourCode, StringComparer.OrdinalIgnoreCase).ToList();
}