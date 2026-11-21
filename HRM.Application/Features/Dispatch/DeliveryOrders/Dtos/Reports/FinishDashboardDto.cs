using HRM.Application.Commons.Pagination;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;

public sealed class FinishDashboardSummary
{
    public decimal? RevenueVnd { get; set; }
    public decimal QuantityKg { get; init; }
    public long Bags { get; init; }
    public int DeliveryCount { get; init; }
    public int OrderCount { get; init; }
    public int CustomerCount { get; init; }
    public int ProductCount { get; init; }
    public int LineCount { get; init; }
}

public sealed record FinishDashboardDay(DateTime Date, decimal QuantityKg, long Bags, int DeliveryCount);
public sealed record FinishDashboardRanking(Guid? Id, string? Name, decimal QuantityKg, long Bags);
public sealed record FinishDashboardDetail(Guid Id, DeliveryFinishRow Data);
public sealed record FinishDashboardDto(DateTime From, DateTime To, string Scope,
    FinishDashboardSummary Summary, IReadOnlyList<FinishDashboardDay> Daily,
    IReadOnlyList<FinishDashboardRanking> Customers, IReadOnlyList<FinishDashboardRanking> Products,
    PagedResult<FinishDashboardDetail> Rows);
