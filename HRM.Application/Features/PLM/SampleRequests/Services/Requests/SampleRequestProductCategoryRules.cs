namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>
/// Stable category codes allowed for new or reclassified Sample Request products.
/// Legacy categories remain readable for historical records but cannot be selected again.
/// </summary>
public static class SampleRequestProductCategoryRules
{
    public const string CompoundCode = "CMP";
    public const string ColorMasterbatchCode = "CMB";
    public const string AdditiveMasterbatchCode = "AMB";
    public const string AdditiveCode = "ADD";
    public const string PigmentCode = "PIG";

    public static readonly string[] CanonicalCategoryCodes =
    [
        CompoundCode,
        ColorMasterbatchCode,
        AdditiveMasterbatchCode,
        PigmentCode,
        "VRG",
        AdditiveCode,
        "GCO"
    ];

    public static bool IsCanonical(string? categoryExternalId)
        => !string.IsNullOrWhiteSpace(categoryExternalId) &&
           CanonicalCategoryCodes.Contains(categoryExternalId, StringComparer.Ordinal);
}
