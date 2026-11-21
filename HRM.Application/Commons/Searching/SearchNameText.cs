using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HRM.Application.Commons.Searching;

internal static class SearchNameText
{
    internal static string[] Words(string value) =>
        value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static string PhrasePattern(string value) => string.Join(@"\s+", Words(value).Select(Pattern));
    // Không dùng cờ inline (?s): Npgsql có thể thêm (?p) trước pattern; PostgreSQL chỉ nhận cờ ở đầu.
    internal static string OrderedPattern(string value) => string.Join(@"(?:.|\n)*", Words(value).Select(Pattern));
    internal static string AllWordsPattern(string value)
    {
        var words = Words(value).Select(Pattern).Distinct(StringComparer.Ordinal).ToArray();
        return words.Length <= 1 ? words.FirstOrDefault() ?? "" :
            "^" + string.Concat(words.Select(word => @"(?=(?:.|\n)*" + word + ")"));
    }

    internal static string Fold(string value) => string.Concat(
        value.ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        .Replace('đ', 'd');

    // Regex được Npgsql dịch xuống PostgreSQL; không cần extension hoặc thay đổi schema.
    internal static string Pattern(string value)
    {
        var result = new StringBuilder();
        foreach (var c in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            result.Append(c switch
            {
                'a' => "[aàáảãạăằắẳẵặâầấẩẫậ]",
                'e' => "[eèéẻẽẹêềếểễệ]",
                'i' => "[iìíỉĩị]",
                'o' => "[oòóỏõọôồốổỗộơờớởỡợ]",
                'u' => "[uùúủũụưừứửữự]",
                'y' => "[yỳýỷỹỵ]",
                'd' or 'đ' => "[dđ]",
                _ => Regex.Escape(c.ToString())
            });
        }
        return result.ToString();
    }
}
