namespace HRM.Application.Features.Reports.Sales.Shared;

public abstract class SalesReportDrawerRequest
{
    public Guid? CompanyId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public Guid? SaleGroup { get; init; }
    public Guid? SalePerson { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    internal bool IsValid() => SaleGroup != Guid.Empty && SalePerson != Guid.Empty
        && PageNumber > 0 && PageSize is >= 1 and <= 100
        && ((long)PageNumber - 1) * PageSize <= int.MaxValue;
}

public sealed class SalesReportDrawerPage<T>
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required IReadOnlyList<T> Items { get; init; }
}
