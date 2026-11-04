using HRM.Application.Features.PLM.Materials.DocumentImport;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HRM.Application.Features.PLM.Materials.TdsExtraction;

public static class TdsExtractionRules
{
    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".jfif", ".txt", ".csv" };

    public static void ValidateFile(string name, long size, TdsExtractionOptions options)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 ||
            name.IndexOfAny(['/', '\\', '\0']) >= 0 || name.Any(char.IsControl))
            throw new TdsProcessingException("invalid_file_name");
        if (!AllowedExtensions.Contains(Path.GetExtension(name)))
            throw new TdsProcessingException("unsupported_file_type");
        if (size <= 0 || size > options.MaxFileBytes)
            throw new TdsProcessingException("file_size_limit");
    }

    public static void ValidateAndMatch(TdsFileResult result, IReadOnlyList<TdsMaterialCandidate> materials,
        TdsNormalizedDocument document)
    {
        var data = result.Data!;
        if (data.Specifications is null || data.MaterialCodes is null || data.Specifications.Count > 300 ||
            data.MaterialCodes.Count > 30 || data.Specifications.Any(x => x is null))
            throw new TdsProcessingException("invalid_extraction_schema");
        if (!string.Equals(data.DocumentType, "TDS", StringComparison.OrdinalIgnoreCase))
            result.Warnings.Add("document_type_requires_review");
        if (string.IsNullOrWhiteSpace(data.ProductName)) result.Warnings.Add("product_name_missing");
        if (data.Specifications.Count == 0) result.Warnings.Add("specifications_missing");
        foreach (var spec in data.Specifications)
        {
            spec.ValidationCodes = [];
            if (string.IsNullOrWhiteSpace(spec.Name) || string.IsNullOrWhiteSpace(spec.Value))
                spec.ValidationCodes.Add("specification_incomplete");
            if (string.IsNullOrWhiteSpace(spec.SourceLocation) || string.IsNullOrWhiteSpace(spec.Evidence))
                spec.ValidationCodes.Add("specification_evidence_missing");
            else if (document.Text is { } text && !text.Contains(spec.Evidence, StringComparison.OrdinalIgnoreCase))
                spec.ValidationCodes.Add("evidence_not_found_in_normalized_text");
            var value = spec.Value ?? "";
            if (value.Contains(',') && value.Any(char.IsDigit)) spec.ValidationCodes.Add("number_format_requires_review");
            var range = Regex.Match(value, @"\A\s*(?<min>-?\d+(?:\.\d+)?)\s*(?:–|—|to|\.\.|-)\s*(?<max>-?\d+(?:\.\d+)?)\s*\z",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (range.Success && decimal.TryParse(range.Groups["min"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var minimum) &&
                decimal.TryParse(range.Groups["max"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var maximum) && minimum > maximum)
                spec.ValidationCodes.Add("numeric_range_reversed");
            if ((range.Success || decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out _)) && string.IsNullOrWhiteSpace(spec.Unit))
                spec.ValidationCodes.Add("numeric_unit_missing");
            result.Warnings.AddRange(spec.ValidationCodes);
        }
        // Numeric ranges, units and conditions remain verbatim: no midpoint or implicit conversion.
        result.Warnings.Add("ai_output_requires_review");
        var filename = MaterialDocumentFileNameParser.Parse(result.FileName);
        result.Warnings.AddRange(filename.Notes.Where(x => x.Contains("ambiguous") || x.Contains("typo")));
        var fileCodes = filename.MaterialCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var contentCodes = data.MaterialCodes.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(MaterialDocumentFileNameParser.NormalizeMaterialCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var codes = fileCodes.Union(contentCodes, StringComparer.OrdinalIgnoreCase).ToArray();
        var exact = materials.Where(x => codes.Contains(MaterialDocumentFileNameParser.NormalizeMaterialCode(x.ExternalId),
            StringComparer.OrdinalIgnoreCase)).ToList();
        if (fileCodes.Count > 0 && contentCodes.Count > 0 && !fileCodes.SetEquals(contentCodes))
            result.Warnings.Add("filename_content_code_conflict");
        result.Candidates = exact;
        if (codes.Length == 1 && exact.Count == 1)
        {
            result.MatchStatus = "exact_match";
            result.MaterialId = exact[0].MaterialId;
            result.MaterialExternalId = exact[0].ExternalId;
        }
        else if (exact.Count > 0) result.MatchStatus = "ambiguous";
        else
        {
            // Name equality only suggests a candidate; it never assigns a material ID.
            result.Candidates = materials.Where(x => !string.IsNullOrWhiteSpace(data.ProductName) &&
                string.Equals(x.Name?.Trim(), data.ProductName.Trim(), StringComparison.OrdinalIgnoreCase)).Take(10).ToList();
            result.MatchStatus = result.Candidates.Count > 0 ? "suggested" : "unmatched";
        }
        result.Warnings = result.Warnings.Distinct(StringComparer.Ordinal).ToList();
    }
}
