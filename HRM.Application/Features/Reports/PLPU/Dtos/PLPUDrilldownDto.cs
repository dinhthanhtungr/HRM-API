namespace HRM.Application.Features.Reports.PLPU;

/// <summary>Đối soát KPI trên toàn bộ cohort; Items chỉ chứa trang hiện tại.</summary>
public sealed class PLPUDrilldownDto
{
    public Guid CompanyId { get; init; }
    public string Metric { get; init; } = string.Empty;
    public string RowType { get; init; } = string.Empty;
    public decimal MetricValue { get; init; }
    public decimal PageValue { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<PLPUDrilldownRowDto> Items { get; init; } = [];
}

public sealed class PLPUDrilldownRowDto
{
    public decimal Contribution { get; init; }
    public string ReportStatusCode { get; init; } = string.Empty;
    public string? LineStatusCode { get; init; }
    public PLPUPurchaseOrderHeaderReportDto Order { get; init; } = new();
    public PLPUPurchaseOrderDetailReportDto? Line { get; init; }
}
