using HRM.Application.Commons.Pricing.Rules;
using System.Text.RegularExpressions;

namespace HRM.Application.Features.DevAndQA.MaterialEquivalences;

public static class MaterialEquivalenceRules
{
    public static readonly Guid PigmentCategoryId = new("d5fdf825-cca1-445c-9cdb-0bae7bd0d75b");

    public static bool IncludesFormula(string type, int distinctMaterialCount, int candidateCount)
        => type == "L" || (distinctMaterialCount == 1 && candidateCount == 1);

    public static bool MatchesProductOnHand(decimal totalKg, decimal? min, decimal? max)
        => (!min.HasValue || totalKg >= min.Value) && (!max.HasValue || totalKg <= max.Value);

    public static string? ProcessingType(string? name)
    {
        var words = Regex.Split(InternalMaterialCostingRules.Normalize(name), "[^A-Z0-9]+");
        var types = new List<string>();
        if (words.Contains("BAM")) types.Add("B");
        if (words.Contains("NGHIEN")) types.Add("N");
        if (words.Contains("LOANG")) types.Add("L");
        return types.Count == 1 ? types[0] : null;
    }

    public static IReadOnlyList<EquivalentMaterialDto> Candidates(string type, IEnumerable<EquivalentMaterialDto> materials)
        => materials.Where(x => type != "L" || x.CategoryId == PigmentCategoryId)
            .DistinctBy(x => x.MaterialId).OrderBy(x => x.Code).ToArray();
}
