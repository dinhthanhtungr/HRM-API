using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Formulas.Services;

/// <summary>
/// Defines lifecycle statuses in which the active Formula material composition is immutable.
/// Header fields remain editable through their normal APIs.
/// </summary>
internal static class FormulaMaterialMutationRules
{
    public static bool CanReplaceMaterials(string? status)
    {
        return !string.Equals(status, FormulaStatus.SampleSent.ToString(), StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(status, FormulaStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(status, FormulaStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasCompositionChanges(
        IReadOnlyList<FormulaMaterialCompositionItem> currentItems,
        IReadOnlyList<FormulaMaterialCompositionItem> requestedItems)
    {
        return !currentItems.SequenceEqual(requestedItems);
    }

    public static void EnsureCanReplaceMaterials(string? status, bool hasCompositionChanges)
    {
        if (!hasCompositionChanges || CanReplaceMaterials(status))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Formula materials cannot be changed when status is {status}.");
    }
}

internal readonly record struct FormulaMaterialCompositionItem(
    ItemType ItemType,
    Guid ItemId,
    decimal Quantity);
