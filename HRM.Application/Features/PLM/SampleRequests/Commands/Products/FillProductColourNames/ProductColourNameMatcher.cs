using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HRM.Domain.ReferenceData.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.FillProductColourNames;

internal static class ProductColourNameMatcher
{
    private static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>
    {
        ["Black"] = ["DEN"], ["Blue"] = ["XANH DUONG", "XANH DA TROI"],
        ["Brown"] = ["NAU"], ["Fluorescent"] = ["HUYNH QUANG"],
        ["Green"] = ["XANH LA", "XANH LUC"], ["Grey"] = ["GRAY", "XAM", "GHI"],
        ["Orange"] = ["CAM"], ["Pearl"] = ["NGOC TRAI"], ["Pink"] = ["HONG"],
        ["Red"] = ["DO"], ["Violet"] = ["PURPLE", "TIM"], ["White"] = ["TRANG"],
        ["Yellow"] = ["VANG"]
    };

    internal static string? Match(string? name)
    {
        var normalized = " " + Normalize(name ?? "") + " ";
        var matches = SampleRequestReferenceData.Colors.Where(color =>
            new[] { color.Value }.Concat(Aliases.GetValueOrDefault(color.Value) ?? [])
                .Any(alias => normalized.Contains(" " + Normalize(alias) + " ", StringComparison.Ordinal)))
            .Select(color => color.Value).Distinct().ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string Normalize(string value)
    {
        var text = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                text.Append(character is 'đ' or 'Đ' ? 'D' : char.ToUpperInvariant(character));
        return Regex.Replace(text.ToString(), "[^A-Z0-9]+", " ").Trim();
    }
}
