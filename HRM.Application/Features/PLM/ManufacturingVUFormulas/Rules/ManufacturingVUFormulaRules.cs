using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Rules;

internal static class ManufacturingVUFormulaRules
{
    public const int MaxNoteLength = 2_000;

    public static bool IsTerminal(ManufacturingProductOrder status)
        => status is ManufacturingProductOrder.Finished
            or ManufacturingProductOrder.Done
            or ManufacturingProductOrder.Stocked
            or ManufacturingProductOrder.Canceled;

    public static bool IsUpdatableStatus(ManufacturingProductOrder status)
        => Enum.IsDefined(status)
            && status is not ManufacturingProductOrder.Unknown
            && status is not ManufacturingProductOrder.Canceled;

    public static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? ValidateText(string? value, string fieldName)
        => value?.Length > MaxNoteLength
            ? $"{fieldName} must not exceed {MaxNoteLength} characters."
            : null;
}
