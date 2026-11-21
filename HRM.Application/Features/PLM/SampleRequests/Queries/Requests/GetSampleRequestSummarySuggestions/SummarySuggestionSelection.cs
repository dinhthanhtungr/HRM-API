namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions;

/// <summary>Chia tối đa 10 vị trí theo lượt giữa các loại, tự nhường chỗ khi một loại hết kết quả.</summary>
internal static class SummarySuggestionSelection
{
    internal const int MaximumItems = 10;

    internal static IReadOnlyList<SummarySuggestionQueries.Candidate> Select(
        IEnumerable<SummarySuggestionQueries.Candidate> candidates)
    {
        var groups = candidates
            .OrderBy(x => x.MatchRank).ThenBy(x => x.Label, StringComparer.Ordinal)
            .ThenBy(x => x.Value, StringComparer.Ordinal)
            .DistinctBy(x => (x.Type, x.Value))
            .GroupBy(x => x.Type)
            .Select(group => new Queue<SummarySuggestionQueries.Candidate>(group))
            .OrderBy(group => group.Peek().MatchRank)
            .ThenBy(group => group.Peek().Type, StringComparer.Ordinal)
            .ToArray();
        var selected = new List<SummarySuggestionQueries.Candidate>(MaximumItems);
        while (selected.Count < MaximumItems)
        {
            var added = false;
            foreach (var group in groups)
            {
                if (group.Count == 0) continue;
                selected.Add(group.Dequeue());
                added = true;
                if (selected.Count == MaximumItems) break;
            }
            if (!added) break;
        }
        return selected.OrderBy(x => x.MatchRank).ThenBy(x => x.Type, StringComparer.Ordinal)
            .ThenBy(x => x.Label, StringComparer.Ordinal).ThenBy(x => x.Value, StringComparer.Ordinal).ToArray();
    }
}
