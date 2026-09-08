namespace HRM.Application.Features.PLM.Boms.Models;

internal sealed class BomItemResolution
{
    private BomItemResolution(IReadOnlyList<BomResolvedItem> items, string? error)
    {
        Items = items;
        Error = error;
    }

    internal IReadOnlyList<BomResolvedItem> Items { get; }
    internal string? Error { get; }

    internal static BomItemResolution Ok(IReadOnlyList<BomResolvedItem> items)
        => new(items, null);

    internal static BomItemResolution Fail(string error)
        => new([], error);
}
