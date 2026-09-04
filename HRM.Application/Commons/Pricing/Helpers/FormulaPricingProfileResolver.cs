using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Commons.Pricing.Helpers;

/// <summary>
/// Keeps the legacy Powder/Compound selection rule while tier definitions come from the database.
/// </summary>
public static class FormulaPricingProfileResolver
{
    public static FormulaPricingProfile Resolve(
        string? colourCode,
        string? code,
        string? additive)
    {
        if (string.Equals(additive?.Trim(), "C", StringComparison.OrdinalIgnoreCase))
        {
            return FormulaPricingProfile.Compound;
        }

        return (colourCode ?? code)?.Trim().EndsWith("C", StringComparison.OrdinalIgnoreCase) == true
            ? FormulaPricingProfile.Compound
            : FormulaPricingProfile.Powder;
    }
}
