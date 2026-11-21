using System.Linq.Expressions;
using System.Text.RegularExpressions;
using HRM.Application.Commons.Pagination;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Commons.Searching;

/// <summary>Dựng điều kiện và đọc tối đa 10 giá trị/field từ IQueryable đã scope. Không dùng cho lookup.</summary>
internal sealed class SmartSearchSchema<T>(params SmartSearchField<T>[] fields)
{
    private const int MaximumSuggestions = 10;
    private Func<IQueryable<T>, IQueryable<T>>? SuggestionScope { get; init; }
    internal SmartSearchSchema<T> FilterSuggestionScope(Func<IQueryable<T>, IQueryable<T>> filter)
        => new(fields) { SuggestionScope = filter };
    internal bool IsScopedKeyword(string? keyword) => Parse(keyword ?? "").Field is not null;

    internal IQueryable<T> ApplyAnyKeyword(IQueryable<T> scoped, string? raw)
    {
        var input = Parse(raw ?? "");
        if (input.Keyword.Length == 0) return input.Field is null ? scoped : scoped.Where(_ => false);
        return scoped.WhereKeyword(input.Keyword, term => {
            var parameter = Expression.Parameter(typeof(T), "row");
            Expression body = Expression.Constant(false);
            foreach (var field in fields.Where(x => input.Field is null || x == input.Field))
                body = Expression.OrElse(body, SmartSearchField<T>.Replace(field.Matches(term.Value), parameter));
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        });
    }

    internal IQueryable<T> ApplyRequest(IQueryable<T> scoped, string? keyword, ISmartSearchRequest? request,
        out string? legacyKeyword) => Apply(scoped, keyword,
            request?.SuggestionKeyword is null ? request?.SearchSelections : null, out legacyKeyword);

    internal IQueryable<T> Apply(IQueryable<T> scoped, string? keyword, string? selections, out string? legacyKeyword)
    {
        legacyKeyword = keyword;
        if (!TrySelections(selections, out var selected)) return scoped.Where(_ => false);
        foreach (var group in selected.GroupBy(x => x.Field))
        {
            var field = fields.SingleOrDefault(x => x.Key == group.Key);
            if (field is null) return scoped.Where(_ => false);
            scoped = scoped.Where(field.EqualsAny(group.Select(x => x.Value).Distinct().ToArray()));
        }
        var parsed = Parse(keyword ?? "");
        if (parsed.Field is not null)
        {
            legacyKeyword = null;
            scoped = parsed.Keyword.Length == 0 ? scoped.Where(_ => false) : scoped.Where(parsed.Field.Matches(parsed.Keyword));
        }
        return scoped;
    }

    internal async Task<PagedResult<TDto>?> SuggestionsAsync<TDto>(IQueryable<T> scoped,
        ISmartSearchRequest request, CancellationToken ct)
    {
        if (request.SuggestionKeyword is null) return null;
        var query = BuildSuggestionQuery(scoped, request);
        var candidates = query is null ? [] : await query.ToListAsync(ct);
        return SuggestionPage<TDto>(Select(candidates));
    }

    internal IQueryable<SmartSearchSuggestion>? BuildSuggestionQuery(IQueryable<T> scoped, ISmartSearchRequest request)
    {
        if (request.SuggestionKeyword is null) return null;
        var input = Parse(request.SuggestionKeyword);
        if (request.SuggestionKeyword.Length > 100 || input.Keyword.Length < 2 ||
            !TrySelections(request.SearchSelections, out var selected)) return null;
        IQueryable<SmartSearchSuggestion>? union = null;
        foreach (var field in fields.Where(x => input.Field is null || x == input.Field))
        {
            // Bỏ điều kiện cùng field để người dùng thêm badge thứ hai theo OR.
            var fieldScope = scoped;
            foreach (var group in selected.Where(x => x.Field != field.Key).GroupBy(x => x.Field))
                fieldScope = fieldScope.Where(fields.Single(x => x.Key == group.Key)
                    .EqualsAny(group.Select(x => x.Value).Distinct().ToArray()));
            fieldScope = SuggestionScope?.Invoke(fieldScope) ?? fieldScope;
            var values = field.Values(fieldScope).Where(x => x != "").Distinct();
            var keyword = input.Keyword;
            IQueryable<SmartSearchSuggestion> branch;
            if (field.IsName)
            {
                var all = SearchNameText.AllWordsPattern(keyword);
                var phrase = SearchNameText.PhrasePattern(keyword);
                var ordered = SearchNameText.OrderedPattern(keyword);
                var multipleWords = SearchNameText.Words(keyword).Length > 1;
                branch = values.WhereKeyword(all, term => value => Regex.IsMatch(value, term.Value, RegexOptions.IgnoreCase))
                    .Select(value => new SmartSearchSuggestion { Field = field.Key, Value = value,
                        MatchRank = Regex.IsMatch(value, "^" + phrase + "$", RegexOptions.IgnoreCase) ? 0 :
                            Regex.IsMatch(value, "^" + phrase, RegexOptions.IgnoreCase) ? 1 :
                            !multipleWords || Regex.IsMatch(value, ordered, RegexOptions.IgnoreCase) ? 2 : 3 });
            }
            else
            {
                var exact = PostgresSearchPattern.ExactLiteral(keyword);
                var prefix = PostgresSearchPattern.PrefixLiteral(keyword);
                branch = values.WhereKeyword(keyword, term => value => EF.Functions.ILike(value,
                        term.ContainsPattern, PostgresSearchPattern.EscapeCharacter))
                    .Select(value => new SmartSearchSuggestion { Field = field.Key, Value = value,
                        MatchRank = EF.Functions.ILike(value, exact, PostgresSearchPattern.EscapeCharacter) ? 0 :
                            EF.Functions.ILike(value, prefix, PostgresSearchPattern.EscapeCharacter) ? 1 : 2 });
            }
            branch = branch.OrderBy(x => x.MatchRank).ThenBy(x => x.Value).Take(MaximumSuggestions);
            union = union is null ? branch : union.Concat(branch);
        }
        return union;
    }

    internal static IReadOnlyList<SmartSearchSuggestion> Select(IEnumerable<SmartSearchSuggestion> candidates)
    {
        var groups = candidates.OrderBy(x => x.MatchRank).ThenBy(x => x.Value, StringComparer.Ordinal)
            .DistinctBy(x => (x.Field, x.Value)).GroupBy(x => x.Field)
            .Select(x => new Queue<SmartSearchSuggestion>(x)).OrderBy(x => x.Peek().MatchRank)
            .ThenBy(x => x.Peek().Field, StringComparer.Ordinal).ToArray();
        var result = new List<SmartSearchSuggestion>();
        while (result.Count < MaximumSuggestions)
        {
            var count = result.Count;
            foreach (var group in groups)
                if (result.Count < MaximumSuggestions && group.TryDequeue(out var item)) result.Add(item);
            if (count == result.Count) break;
        }
        return result.OrderBy(x => x.MatchRank).ThenBy(x => x.Field, StringComparer.Ordinal)
            .ThenBy(x => x.Value, StringComparer.Ordinal).ToArray();
    }

    private (SmartSearchField<T>? Field, string Keyword) Parse(string raw)
    {
        var trimmed = raw.Trim();
        var colon = trimmed.IndexOfAny([':', '：']);
        if (colon < 0) return (null, trimmed);
        var label = Normalize(trimmed[..colon]);
        var field = fields.FirstOrDefault(x => Normalize(x.Key) == label || x.Aliases.Any(alias => Normalize(alias) == label));
        return field is null ? (null, trimmed) : (field, trimmed[(colon + 1)..].Trim());
    }

    private static string Normalize(string value) => string.Join(' ', SearchNameText.Words(SearchNameText.Fold(value)));

    private bool TrySelections(string? raw, out SmartSearchSelection[] selections)
        => SmartSearchSelections.TryParse(raw, fields.Select(x => x.Key), out selections);

    private static PagedResult<TDto> SuggestionPage<TDto>(IReadOnlyList<SmartSearchSuggestion> items)
        => new([], 0, 1, MaximumSuggestions) { SearchSuggestions = items };
}
