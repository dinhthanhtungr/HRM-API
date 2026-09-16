namespace HRM.Application.Features.PLM.Boms.Services;

/// <summary>
/// Keeps Formula-origin metadata in the existing BOM change-reason field.
/// No database column or FK is required for the Formula-to-M-BOM bootstrap flow.
/// </summary>
internal static class FormulaDrivenManufacturingBomMarker
{
    private const string Prefix = "formula-driven:";

    internal static string Create(Guid formulaId) => $"{Prefix}{formulaId:N}";

    internal static bool IsFormulaDriven(string? changeReason) =>
        TryGetFormulaId(changeReason).HasValue;

    internal static Guid? TryGetFormulaId(string? changeReason)
    {
        if (string.IsNullOrWhiteSpace(changeReason) ||
            !changeReason.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Guid.TryParseExact(changeReason[Prefix.Length..], "N", out var formulaId)
            ? formulaId
            : null;
    }
}
