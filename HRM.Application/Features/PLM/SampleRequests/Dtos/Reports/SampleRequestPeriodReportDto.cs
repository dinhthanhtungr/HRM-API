namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

public sealed class SampleRequestPeriodReportDto
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public int ApprovedTrialCount { get; init; }
    public int FailedTrialCount { get; init; }
    public int CancelledTrialCount { get; init; }
    /// <summary>Current delay flags whose relevant due date is in the selected period; not historical delay events.</summary>
    public int CurrentlyDelayedSampleRequestCount { get; init; }
    public bool HasHistoricalDelayData => false;
    public bool HasStructuredDelayReasons => false;
}
