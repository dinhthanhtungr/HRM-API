namespace HRM.Application.Features.Reports.DeliveryOtd;

public enum DeliveryOtdDateBasis { Requested, Promised }
public enum DeliveryOtdStatus { MissingDueDate, NotDue, OnTime, Late, OverdueIncomplete }

public class DeliveryOtdFilter
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public DateTime? AsOf { get; init; }
    public DeliveryOtdDateBasis DateBasis { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? ManagerId { get; init; }
}

public sealed record DeliveryOtdSummary(int TotalLines, int DueLines, int OnTimeLines,
    int LateLines, int OverdueIncompleteLines, int NotDueLines, int MissingDueDateLines,
    decimal DueQuantity, decimal OnTimeQuantity, decimal OverdueRemainingQuantity,
    decimal? OtdPercent, decimal? QuantityOtdPercent, decimal? AverageLateDays);

public sealed record DeliveryOtdHeaderDto(DateTime From, DateTime To, DateTime AsOf,
    DeliveryOtdDateBasis DateBasis, DeliveryOtdSummary Summary)
{
    public DeliveryOtdPoSummary PoSummary { get; init; } = new(0, 0, 0, 0, 0, null);
    public decimal DeliveredQuantityInPeriod { get; init; }
    public string EvaluationUnit => "OrderLine";
    public string DeliveryDateSource => "DeliveryOrderCreatedDate";
    public bool IsCustomerReceiptConfirmed => false;
    public bool IsHistoricalSnapshot => false;
}

public sealed record DeliveryOtdDetailDto(Guid DetailId, Guid OrderId, string OrderCode,
    Guid CustomerId, string CustomerName, Guid ProductId, string ProductName,
    DateTime RequestedDate, DateTime? PromisedDate, DateTime? DueDate,
    decimal OrderedQuantity, decimal DeliveredQuantity, decimal OnTimeQuantity,
    decimal RemainingQuantity, DateTime? CompletedDate, int? LateDays,
    int OverdueDays, bool IsDeliveryPaused, DeliveryOtdStatus Status);

public sealed record DeliveryOtdChartPoint(string Key, string Label, DeliveryOtdSummary Summary)
{
    public DeliveryOtdPoSummary? PoSummary { get; init; }
}
public sealed record DeliveryOtdChartsDto(IReadOnlyList<DeliveryOtdChartPoint> Monthly,
    IReadOnlyList<DeliveryOtdChartPoint> Customers, IReadOnlyList<DeliveryOtdChartPoint> Products,
    IReadOnlyList<DeliveryOtdChartPoint> Statuses, IReadOnlyList<DeliveryOtdChartPoint> Aging);
public sealed record DeliveryOtdDetailsDto(int TotalCount, int Page, int PageSize,
    IReadOnlyList<DeliveryOtdDetailDto> Items);

public sealed record DeliveryOtdPoSummary(int TotalPos, int EvaluatedPos, int OnTimePos,
    int LatePos, int PendingPos, decimal? PoOtdPercent);

internal sealed record DeliveryOtdPoDates(Guid OrderId, DateTime? PromisedDate, DateTime? LatestDeliveryDate,
    bool HasDeliveryPauseHistory = false);
