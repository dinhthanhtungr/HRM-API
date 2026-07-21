using System.Collections.ObjectModel;

namespace HRM.Domain.ReferenceData.SampleRequests;

public static class SampleRequestReferenceData
{
    public static readonly IReadOnlyList<SampleRequestColorDefinition> Colors =
        new ReadOnlyCollection<SampleRequestColorDefinition>(new[]
        {
            new SampleRequestColorDefinition("Black", "Black"),
            new SampleRequestColorDefinition("Blue", "Blue"),
            new SampleRequestColorDefinition("Brown", "Brown"),
            new SampleRequestColorDefinition("Fluorescent", "Fluorescent"),
            new SampleRequestColorDefinition("Green", "Green"),
            new SampleRequestColorDefinition("Grey", "Grey"),
            new SampleRequestColorDefinition("Orange", "Orange"),
            new SampleRequestColorDefinition("Pearl", "Pearl"),
            new SampleRequestColorDefinition("Pink", "Pink"),
            new SampleRequestColorDefinition("Red", "Red"),
            new SampleRequestColorDefinition("Tint", "Tint"),
            new SampleRequestColorDefinition("Violet", "Violet"),
            new SampleRequestColorDefinition("White", "White"),
            new SampleRequestColorDefinition("Yellow", "Yellow")
        });

    public static readonly IReadOnlyList<SampleRequestAdditiveDefinition> Additives =
        new ReadOnlyCollection<SampleRequestAdditiveDefinition>(new[]
        {
            new SampleRequestAdditiveDefinition("A_AB", "A", "Anti-Block"),
            new SampleRequestAdditiveDefinition("A_AS", "A", "Anti-Static"),
            new SampleRequestAdditiveDefinition("A_ASC", "A", "Anti-Scratch"),
            new SampleRequestAdditiveDefinition("A_ASL", "A", "Anti-Slip"),
            new SampleRequestAdditiveDefinition("A_AFG", "A", "Anti-Fogging"),
            new SampleRequestAdditiveDefinition("A_AMB", "A", "Anti-Microbial"),
            new SampleRequestAdditiveDefinition("A_AOX", "A", "Anti-Oxidant"),
            new SampleRequestAdditiveDefinition("Y_CLA", "Y", "Clarifying Agent"),
            new SampleRequestAdditiveDefinition("R_FR", "R", "Flame Retardant"),
            new SampleRequestAdditiveDefinition("I_IM", "I", "Impact Modifier"),
            new SampleRequestAdditiveDefinition("N_NA", "N", "Nucleating Agent"),
            new SampleRequestAdditiveDefinition("O_OB", "O", "Optical Brightener"),
            new SampleRequestAdditiveDefinition("P_PA", "P", "Process Acid"),
            new SampleRequestAdditiveDefinition("S_SC", "S", "Scent"),
            new SampleRequestAdditiveDefinition("U_UV", "U", "UV Protection"),
            new SampleRequestAdditiveDefinition("L_SL", "L", "Slip"),
            new SampleRequestAdditiveDefinition("J_ST", "J", "Sterate"),
            new SampleRequestAdditiveDefinition("C_CP", "C", "Compound"),
            new SampleRequestAdditiveDefinition("D_DC", "D", "Dry Colour")
        });

    public static SampleRequestColorDefinition? FindColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Colors.FirstOrDefault(x =>
            string.Equals(x.Value, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static SampleRequestAdditiveDefinition? FindAdditive(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return Additives.FirstOrDefault(x =>
            string.Equals(x.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
