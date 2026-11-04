using System.Text.RegularExpressions;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.DefaultMembership;

internal static partial class DefaultMembershipRuleValidation
{
    internal static bool ValidKind(InternalConversationGroupType kind) => kind is InternalConversationGroupType.Technical or InternalConversationGroupType.Pricing;

    internal static string? Normalize(IReadOnlyList<DefaultMembershipRuleDto>? input, out DefaultMembershipRuleDto[] rules)
    {
        rules = [];
        if (input is null || input.Count > 100) return "RulesRequiredOrLimitExceeded";
        var normalized = new List<DefaultMembershipRuleDto>();
        foreach (var rule in input)
        {
            if (rule is null || !Enum.IsDefined(rule.SourceType) || !Enum.IsDefined(rule.CategoryScope)) return "InvalidRuleCode";
            var code = string.IsNullOrWhiteSpace(rule.GroupTypeCode) ? null : rule.GroupTypeCode.Trim().ToUpperInvariant();
            var roleSource = rule.SourceType is DefaultMembershipRuleSource.CompanyRole or DefaultMembershipRuleSource.RootParticipantRole;
            if (roleSource && (rule.RoleId is null || rule.RoleId == Guid.Empty || code is not null)) return "RoleSourceFieldsInvalid";
            if (!roleSource && (rule.RoleId is not null || code is null || !GroupCode().IsMatch(code))) return "LabSourceFieldsInvalid";
            if (rule.CategoryScope == DefaultMembershipCategoryScope.Specific)
            {
                if (rule.ProductCategoryId is null || rule.ProductCategoryId == Guid.Empty) return "ProductCategoryRequired";
            }
            else if (rule.ProductCategoryId is not null) return "UnexpectedProductCategory";
            if (rule.CategoryScope == DefaultMembershipCategoryScope.Fallback && roleSource) return "FallbackOnlyForLabLeader";
            normalized.Add(rule with { GroupTypeCode = code });
        }
        if (normalized.Distinct().Count() != normalized.Count) return "DuplicateRule";
        rules = normalized.ToArray();
        return null;
    }

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex GroupCode();
}
