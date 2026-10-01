using HRM.Application.Features.PLM.Formulas.Dtos.Commons;

namespace HRM.Application.Features.PLM.Formulas.Services;

internal static class FormulaPricingReviewRules
{
    public static bool HasMaterialChanges(
        IReadOnlyList<FormulaMaterialCompositionItem> current,
        IReadOnlyList<UpsertFormulaMaterialRequest> requested)
    {
        // Ignore line order and client snapshots/prices; compare the persisted composition.
        var next = requested.Select(x => new FormulaMaterialCompositionItem(
            x.ItemType, x.ItemId, Math.Round(x.Quantity, 10, MidpointRounding.AwayFromZero)));
        return !Sort(current).SequenceEqual(Sort(next));
    }

    private static IEnumerable<FormulaMaterialCompositionItem> Sort(IEnumerable<FormulaMaterialCompositionItem> items)
        => items.OrderBy(x => x.ItemType).ThenBy(x => x.ItemId).ThenBy(x => x.Quantity);
}
