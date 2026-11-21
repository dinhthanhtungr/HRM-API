using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions;
using Candidate = HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions.SummarySuggestionQueries.Candidate;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SummarySuggestionSelectionTests
{
    private static IEnumerable<Candidate> Items(string type, int count, int rank = 2) =>
        Enumerable.Range(0, count).Select(i => new Candidate
        {
            Type = type, Value = type + i, Label = type + i.ToString("D2"), MatchRank = rank
        });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    public void SharesTenPlacesAcrossAvailableTypes(int types)
    {
        var candidates = Enumerable.Range(0, types).SelectMany(i => Items("type" + i, 10));
        var selected = SummarySuggestionSelection.Select(candidates);
        Assert.Equal(10, selected.Count);
        var counts = selected.GroupBy(x => x.Type).Select(x => x.Count()).ToArray();
        Assert.Equal(types, counts.Length);
        Assert.InRange(counts.Max() - counts.Min(), 0, 1);
    }

    [Fact]
    public void AShortTypeGivesItsUnusedPlacesToOthers()
    {
        var selected = SummarySuggestionSelection.Select(Items("employee", 1, 0).Concat(Items("product", 10)));
        Assert.Equal(1, selected.Count(x => x.Type == "employee"));
        Assert.Equal(9, selected.Count(x => x.Type == "product"));
        Assert.Equal(0, selected[0].MatchRank);
    }

    [Fact]
    public void BetterMatchingTypeGetsTheExtraPlace()
    {
        var selected = SummarySuggestionSelection.Select(
            Items("customer", 10, 2).Concat(Items("product", 10, 0)).Concat(Items("formula", 10, 1)));
        Assert.Equal(4, selected.Count(x => x.Type == "product"));
        Assert.Equal(3, selected.Count(x => x.Type == "formula"));
        Assert.Equal(3, selected.Count(x => x.Type == "customer"));
    }

    [Fact]
    public void DoesNotInventResultsOrRepeatTheSameChoice()
    {
        Assert.Empty(SummarySuggestionSelection.Select([]));
        var input = Items("product", 2).ToArray();
        var selected = SummarySuggestionSelection.Select(input.Concat(input));
        Assert.Equal(2, selected.Count);
        Assert.Equal(2, selected.Select(x => (x.Type, x.Value)).Distinct().Count());
    }

    [Fact]
    public void OrderingAndAllocationAreStableWhenDatabaseInputOrderChanges()
    {
        var input = Items("product", 10).Concat(Items("customer", 10)).ToArray();
        Assert.Equal(
            SummarySuggestionSelection.Select(input).Select(x => (x.Type, x.Value)),
            SummarySuggestionSelection.Select(input.Reverse()).Select(x => (x.Type, x.Value)));
    }
}
