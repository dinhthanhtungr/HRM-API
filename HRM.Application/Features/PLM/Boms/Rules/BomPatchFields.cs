namespace HRM.Application.Features.PLM.Boms.Rules;

internal static class BomPatchFields
{
    internal const string EffectiveFrom = "effectiveFrom";
    internal const string EffectiveTo = "effectiveTo";
    internal const string ChangeReason = "changeReason";
    internal const string Note = "note";

    internal static IReadOnlySet<string> Supported { get; } = new HashSet<string>(
        [EffectiveFrom, EffectiveTo, ChangeReason, Note],
        StringComparer.OrdinalIgnoreCase);
}
