namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

internal sealed class SampleCustomerPriorityFact
{
    public Guid CustomerId { get; init; }
    public string? Code { get; init; }
    public bool Pinned { get; init; }
    public int Rank { get; init; }
    public bool Overdue { get; init; }
    public DateTime? Due { get; init; }
}

/// <summary>Receives facts in PostgreSQL code/ID order; memory is bounded by customer count.</summary>
internal sealed class SampleCustomerPriorityPage
{
    private readonly Dictionary<Guid, (SampleCustomerPriorityFact Fact, int TieOrder)> _customers = [];

    internal void Include(SampleCustomerPriorityFact fact)
    {
        if (!_customers.TryGetValue(fact.CustomerId, out var current))
            _customers.Add(fact.CustomerId, (fact, _customers.Count));
        else if (Compare(fact, current.Fact) < 0)
            _customers[fact.CustomerId] = (fact, current.TieOrder);
    }

    internal Guid[] GetIds(int pageNumber, int pageSize)
        => _customers.Values.OrderByDescending(x => x.Fact.Pinned).ThenBy(x => x.Fact.Rank).ThenByDescending(x => x.Fact.Overdue)
            .ThenBy(x => x.Fact.Due == null).ThenBy(x => x.Fact.Due).ThenBy(x => x.TieOrder)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(x => x.Fact.CustomerId).ToArray();

    private static int Compare(SampleCustomerPriorityFact left, SampleCustomerPriorityFact right)
    {
        var pin = right.Pinned.CompareTo(left.Pinned);
        if (pin != 0) return pin;
        var rank = left.Rank.CompareTo(right.Rank);
        if (rank != 0) return rank;
        var overdue = right.Overdue.CompareTo(left.Overdue);
        if (overdue != 0) return overdue;
        if (left.Due == right.Due) return 0;
        if (left.Due is null) return 1;
        if (right.Due is null) return -1;
        return left.Due.Value.CompareTo(right.Due.Value);
    }
}
