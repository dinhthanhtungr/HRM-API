using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Boms.Rules;

internal static class BomRules
{
    internal static string? ValidateCreateRequest(CreateBomRequest request)
        => ValidateText(request.Code, 64, nameof(request.Code), required: true)
            ?? ValidateText(request.Name, 200, nameof(request.Name), required: true)
            ?? ValidateText(request.OutputUnit, 32, nameof(request.OutputUnit), required: true)
            ?? ValidatePeriod(request.EffectiveFrom, request.EffectiveTo)
            ?? ValidateItems(request.Items)
            ?? ValidatePositiveBaseOutputQuantity(request.BaseOutputQuantity);

    internal static string? ValidateReplaceRequest(ReplaceBomVersionRequest request)
        => ValidateText(request.OutputUnit, 32, nameof(request.OutputUnit), required: true)
            ?? ValidatePeriod(request.EffectiveFrom, request.EffectiveTo)
            ?? ValidateItems(request.Items)
            ?? ValidatePositiveBaseOutputQuantity(request.BaseOutputQuantity);

    internal static string? ValidatePeriod(DateTime? effectiveFrom, DateTime? effectiveTo)
        => effectiveTo is not null &&
           effectiveFrom is not null &&
           effectiveTo <= effectiveFrom
            ? "EffectiveTo must be later than EffectiveFrom."
            : null;

    internal static string? ValidatePatchClearFields(IEnumerable<string> clearFields)
        => clearFields.Except(BomPatchFields.Supported, StringComparer.OrdinalIgnoreCase).Any()
            ? "ClearFields contains an unsupported field."
            : null;

    internal static string? ValidateText(
        string? value,
        int maxLength,
        string fieldName,
        bool required = false)
    {
        var normalized = value?.Trim();
        if (required && string.IsNullOrWhiteSpace(normalized))
        {
            return $"{fieldName} is required.";
        }

        return normalized is { Length: > 0 } && normalized.Length > maxLength
            ? $"{fieldName} cannot exceed {maxLength} characters."
            : null;
    }

    internal static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ValidatePositiveBaseOutputQuantity(decimal quantity)
        => quantity <= 0
            ? "BaseOutputQuantity must be greater than zero."
            : null;

    private static string? ValidateItems(IReadOnlyList<BomItemWriteDto> items)
    {
        if (items.Count == 0)
        {
            return "At least one BOM item is required.";
        }

        if (items.Count > 500)
        {
            return "A BOM version cannot contain more than 500 items.";
        }

        foreach (var item in items)
        {
            if (item.ItemId == Guid.Empty ||
                item.Quantity <= 0 ||
                string.IsNullOrWhiteSpace(item.Unit) ||
                item.Unit.Length > 32)
            {
                return "Every item requires ItemId, a positive Quantity, and Unit up to 32 characters.";
            }

            if (item.ItemType is not (ItemType.Material or ItemType.Product))
            {
                return "E-BOM items must be Material or Product.";
            }
        }

        return null;
    }
}
