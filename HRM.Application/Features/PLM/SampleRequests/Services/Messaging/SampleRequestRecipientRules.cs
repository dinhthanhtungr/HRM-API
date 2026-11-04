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

    public static readonly IReadOnlyList<string> RequiredMessageRecipientUserNames = new[]
    {
        "qaqcad01"
    };

    public static readonly IReadOnlyList<string> SilentWatcherRoleNames = new[]
    {
        ApplicationRoles.Lab.LabUser,
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

        var teams = SampleRequestLabTeamRules.Resolve(normalized);
        // Thông báo vẫn gửi cả hai trưởng nhóm khi chưa phân loại được như contract hiện có.
        return teams.Length == 0 ? RequiredLeaderGroupTypes : teams;
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
