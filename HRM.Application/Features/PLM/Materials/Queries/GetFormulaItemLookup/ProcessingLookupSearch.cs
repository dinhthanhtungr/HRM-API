using System.Text.RegularExpressions;

namespace HRM.Application.Features.PLM.Materials.Queries.GetFormulaItemLookup;

/// <summary>Only an explicit processing qualifier opts into virtual processing choices.</summary>
internal static class ProcessingLookupSearch
{
    public static (string? Keyword, string? ProcessingType) Parse(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return (null, null);
        keyword = keyword.Trim();
        var match = Regex.Match(keyword, @"(?:^|\s)(băm|bam|nghiền|nghien)\s*$|[-_]([BN])$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return (keyword, null);

        var qualifier = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        var type = qualifier.StartsWith("b", StringComparison.OrdinalIgnoreCase) ? "B" : "N";
        var rootKeyword = keyword[..match.Index].Trim();
        return (rootKeyword.Length == 0 ? null : rootKeyword, type);
    }
}
