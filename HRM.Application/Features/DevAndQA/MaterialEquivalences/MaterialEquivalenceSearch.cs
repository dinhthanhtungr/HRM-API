using System.Text.RegularExpressions;
using HRM.Application.Commons.Searching;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.DevAndQA.MaterialEquivalences;

internal static class MaterialEquivalenceSearch
{
    private sealed record Field(string Key, bool Name, string[] Aliases, Func<MaterialEquivalenceDto, IEnumerable<string>> Values);
    private static readonly Field[] Fields = [
        new("sampleRequestCode", false, ["TP Code", "Mã TP"], x => [x.ProductCode]),
        new("colourCode", false, ["Colorcode", "Colour code", "Mã màu"], x => [x.ColourCode]),
        new("productName", true, ["Tên sản phẩm", "Product name"], x => [x.ProductName]),
        new("formulaCode", false, ["Công thức", "Formula code"], x => [x.FormulaCode ?? ""]),
        new("materialCode", false, ["Mã NVL", "Material code"], x => x.Candidates.Select(m => m.Code)),
        new("materialName", true, ["Tên NVL", "Material name"], x => x.Candidates.Select(m => m.Name))
    ];

    // SQL first; refine after the existing business rules choose the actual formula/candidates.
    internal static SmartSearchSchema<Product> Build(IQueryable<Product> query, Guid company) => SmartSearch.For(query, f => [
        f.Codes("sampleRequestCode", x => x.SampleRequests.Where(s => s.CompanyId == company && s.IsActive), s => s.ExternalId, "TP Code", "Mã TP"),
        f.Code("colourCode", x => x.ColourCode, "Colorcode", "Colour code", "Mã màu"),
        f.Name("productName", x => x.Name, "Tên sản phẩm", "Product name"),
        f.Codes("formulaCode", x => x.Formulas.Where(s => s.CompanyId == company && s.IsActive), s => s.ExternalId, "Công thức", "Formula code"),
        f.Codes("materialCode", x => x.Formulas.Where(s => s.CompanyId == company && s.IsActive).SelectMany(s => s.FormulaMaterials)
            .Where(m => m.IsActive && m.MaterialId != null && m.Material.CompanyId == company && m.Material.IsActive == true), m => m.Material.ExternalId, "Mã NVL", "Material code"),
        f.Names("materialName", x => x.Formulas.Where(s => s.CompanyId == company && s.IsActive).SelectMany(s => s.FormulaMaterials)
            .Where(m => m.IsActive && m.MaterialId != null && m.Material.CompanyId == company && m.Material.IsActive == true), m => m.Material.Name, "Tên NVL", "Material name")
    ]);

    internal static List<MaterialEquivalenceDto> Apply(List<MaterialEquivalenceDto> rows, GetMaterialEquivalencesQuery request)
    {
        if (request.SuggestionKeyword is not null) return rows;
        if (!SmartSearchSelections.TryParse(request.SearchSelections, Fields.Select(x => x.Key), out var selected)) return [];
        var input = Parse(request.Keyword ?? "");
        return rows.Where(row => Selected(row, selected) && (input.Field is null ||
            input.Keyword.Length > 0 && input.Field.Values(row).Any(v => Matches(v, input.Keyword, input.Field.Name)))).ToList();
    }

    internal static IReadOnlyList<SmartSearchSuggestion> Suggest(IEnumerable<MaterialEquivalenceDto> rows, GetMaterialEquivalencesQuery request)
    {
        var raw = request.SuggestionKeyword ?? "";
        var input = Parse(raw);
        if (raw.Length > 100 || input.Keyword.Length < 2 ||
            !SmartSearchSelections.TryParse(request.SearchSelections, Fields.Select(x => x.Key), out var selected)) return [];
        return SmartSearchSchema<MaterialEquivalenceDto>.Select(Fields.Where(f => input.Field is null || input.Field == f).SelectMany(field =>
            rows.Where(row => Selected(row, selected, field.Key)).SelectMany(field.Values).Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct().Where(v => Matches(v, input.Keyword, field.Name)).Select(v => new SmartSearchSuggestion {
                    Field = field.Key, Value = v, MatchRank = Rank(v, input.Keyword, field.Name)
                }).OrderBy(x => x.MatchRank).ThenBy(x => x.Value).Take(10)));
    }

    private static bool Selected(MaterialEquivalenceDto row, SmartSearchSelection[] selected, string? except = null)
        => selected.Where(x => x.Field != except).GroupBy(x => x.Field).All(group =>
            Fields.Single(f => f.Key == group.Key).Values(row).Any(value => group.Any(s => s.Value == value)));
    private static bool Matches(string value, string keyword, bool name) => name
        ? Regex.IsMatch(value, SearchNameText.AllWordsPattern(keyword), RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50))
        : value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    private static int Rank(string value, string keyword, bool name)
    {
        var text = name ? SearchNameText.Fold(value) : value;
        var query = name ? SearchNameText.Fold(keyword) : keyword;
        return text.Equals(query, StringComparison.OrdinalIgnoreCase) ? 0 : text.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }
    private static (Field? Field, string Keyword) Parse(string raw)
    {
        var colon = raw.IndexOfAny([':', '：']);
        var field = colon < 0 ? null : Fields.FirstOrDefault(f => f.Key.Equals(raw[..colon].Trim(), StringComparison.OrdinalIgnoreCase) || f.Aliases.Any(a =>
            SearchNameText.Fold(a).Trim().Equals(SearchNameText.Fold(raw[..colon]).Trim(), StringComparison.OrdinalIgnoreCase)));
        return (field, (field is null ? raw : raw[(colon + 1)..]).Trim());
    }
}
