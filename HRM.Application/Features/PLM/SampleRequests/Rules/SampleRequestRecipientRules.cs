using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

public static class SampleRequestRecipientRules
{
    private const string RdGroupType = "QAQC.RD";
    private const string ColorGroupType = "QAQC.MAU";

    public static readonly IReadOnlyList<string> RequiredMessageRecipientRoles = new[]
    {
        ApplicationRoles.President,
        ApplicationRoles.Lab.LabAdmin
    };

    public static readonly IReadOnlyList<string> RequiredLeaderGroupTypes = new[]
    {
        RdGroupType,
        ColorGroupType
    };

    public static IReadOnlyList<string> ResolveDefaultLeaderGroupTypes(string? productCategoryExternalId)
    {
        var normalized = NormalizeBusinessText(productCategoryExternalId);

        return normalized switch
        {
            "PC" => new[] { RdGroupType, ColorGroupType },
            "PMA" or "PPG" => new[] { RdGroupType },
            "PHM" or "PBM" or "PDM" => new[] { ColorGroupType },
            _ => RequiredLeaderGroupTypes
        };
    }

    private static string NormalizeBusinessText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) ==
                System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (!char.IsWhiteSpace(character) && character != '_' && character != '-')
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
