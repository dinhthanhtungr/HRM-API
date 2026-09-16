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

    public static readonly IReadOnlyList<SampleRequestBranchDefinition> Branches =
        new ReadOnlyCollection<SampleRequestBranchDefinition>(new[]
        {
            new SampleRequestBranchDefinition(
                new Guid("f54b3c96-4faa-43d1-8446-9d98c459c630"),
                "VIETAUS_TAM_PHUOC",
                "Vietaus Tam Phước"),
            new SampleRequestBranchDefinition(
                new Guid("4b733a32-9fc7-4d42-810e-3b0be22c4993"),
                "VIETAUS_BINH_DUONG",
                "Vietaus Bình Dương"),
            new SampleRequestBranchDefinition(
                new Guid("7cc1005a-aa68-49b9-91b9-9e0e81260bb1"),
                "VIETAUS_HEAD_OFFICE",
                "Tổng công ty Vietaus"),
            new SampleRequestBranchDefinition(
                new Guid("28420bba-d609-4269-aab4-ff67856bc91e"),
                "VIETAUS_DA_NANG",
                "VU/DN"),
            new SampleRequestBranchDefinition(
                new Guid("be06d383-38cf-4848-afbe-2a8a535143e6"),
                "LONG_GIANG",
                "Long Giang"),
            new SampleRequestBranchDefinition(
                new Guid("312c2a62-c571-4e7b-a0a6-e0c848789279"),
                "A_CHAU",
                "Á Châu"),
            new SampleRequestBranchDefinition(
                new Guid("a7ccf5b9-1db4-4a3e-8071-e391b6e0ad24"),
                "OTHER",
                "Other"),
            new SampleRequestBranchDefinition(
                new Guid("02a65ec6-507b-4250-bf6a-193e02b3c6d5"),
                "VIETAUS_HA_NOI",
                "VU/HN"),
            new SampleRequestBranchDefinition(
                new Guid("e53646b5-ceb1-495a-ad0c-5cb720e0c2a3"),
                "OVERSEAS",
                "Nước Ngoài")
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
