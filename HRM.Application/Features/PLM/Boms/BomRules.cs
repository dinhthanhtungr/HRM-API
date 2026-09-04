using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Boms;

internal static class BomRules
{
    internal static string? ValidatePeriod(DateTime? from, DateTime? to)
        => to is not null && from is not null && to <= from ? "EffectiveTo must be later than EffectiveFrom." : null;

    internal static string? ValidateItems(IReadOnlyList<BomItemWriteDto> items)
    {
        if (items.Count == 0) return "At least one BOM item is required.";
        if (items.Count > 500) return "A BOM version cannot contain more than 500 items.";
        foreach (var item in items)
        {
            if (item.ItemId == Guid.Empty || item.Quantity <= 0 || string.IsNullOrWhiteSpace(item.Unit) || item.Unit.Length > 32)
                return "Every item requires ItemId, a positive Quantity, and Unit up to 32 characters.";
            if (item.ItemType is not (ItemType.Material or ItemType.Product))
                return "E-BOM items must be Material or Product.";
        }
        return null;
    }

    internal static string? Normalize(string? value, int maxLength, string field, bool required = false)
    {
        var normalized = value?.Trim();
        if (required && string.IsNullOrWhiteSpace(normalized)) return $"{field} is required.";
        return normalized is { Length: > 0 } && normalized.Length > maxLength ? $"{field} cannot exceed {maxLength} characters." : null;
    }
}
