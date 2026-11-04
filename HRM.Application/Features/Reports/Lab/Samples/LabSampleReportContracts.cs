namespace HRM.Application.Features.Reports.Lab.Samples;

public class LabSampleReportFilter
{
    public Guid? CompanyId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public Guid? SaleEmployeeId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? LabReceivedByEmployeeId { get; set; }
    public string? RequestType { get; set; }
}

public enum LabSampleMetric
{
    All, Processing, Completed, Sent, WaitingFeedback, Accepted, Rejected,
    Won, LeadTimeValid, LeadTimeMissing, LeadTimeInvalid, ConversionUnknown, OtherStatus
}

public sealed class LabSampleReportDto
{
    public string Timezone { get; init; } = LabSampleReportPeriods.Timezone;
    public LabSamplePeriodDto OverviewPeriod { get; init; } = new(default, default);
    public LabSampleTrendPeriodDto TrendPeriod { get; init; } = new(default, default, 6);
    public string ConversionBasis { get; init; } = "customer_approval_for_po";
    public LabSampleTotals Overview { get; init; } = new();
    public IReadOnlyList<LabSampleMonthDto> Months { get; init; } = [];
}

public sealed record LabSamplePeriodDto(DateOnly From, DateOnly To);
public sealed record LabSampleTrendPeriodDto(DateOnly From, DateOnly To, int MonthCount);

public sealed record LabSampleMonthDto(int Year, int Month, LabSampleTotals Metrics)
{
    public DateOnly PeriodFrom { get; init; }
    public DateOnly PeriodTo { get; init; }
    public bool IsPartialMonth { get; init; }
    public string DataStatus { get; init; } = "no_requests";
    public string? DataUnavailableReason { get; init; }
}

public sealed class LabSampleTotals
{
    public int RequestCount { get; init; }
    public int LeadTimeValidCount { get; init; }
    public int LeadTimeMissingCount { get; init; }
    public int LeadTimeInvalidCount { get; init; }
    public int LeadTimeEligibleCount { get; init; }
    public string? LeadTimeUnavailableReason { get; init; }
    public double? DevelopmentLeadTimeDays { get; init; }
    public int WinNumerator { get; init; }
    public int WinDenominator { get; init; }
    public decimal? WinPercentage { get; init; }
    public int ConversionUnknownCount { get; init; }
    public string? WinUnavailableReason { get; init; }
}

public sealed class LabSampleRowDto
{
    public Guid SampleRequestId { get; init; }
    public string ExternalId { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string? ColourCode { get; init; }
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = "";
    public Guid? LabReceivedByEmployeeId { get; init; }
    public string? LabReceivedByEmployeeName { get; init; }
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime? LabReceivedAt { get; init; }
    public DateTime? ColourAssignedAt { get; init; }
    public DateTime? LabApprovedAt { get; init; }
    public DateTime? CustomerApprovedAt { get; init; }
    public int TrialCount { get; init; }
    public double? DevelopmentLeadTimeDays { get; set; }
    public bool? ApprovedForPo { get; set; }
    // Không suy diễn mã PO từ customer/product. Chưa có quan hệ trực tiếp đáng tin cậy.
    public IReadOnlyList<string>? RelatedOrderCodes { get; init; }
    public bool WaitingFeedback { get; init; }
    public bool Accepted { get; init; }
    public bool Rejected { get; init; }
    public bool ConversionUnknown { get; init; }
}
