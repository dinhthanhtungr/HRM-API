namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

public sealed class SampleRequestAdminSummaryDto
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public string GroupSource => "SampleRequestBranch";
    public bool HasCompleteHistoricalStatus => false;
    public IReadOnlyList<SampleRequestAdminSummaryRowDto> Rows { get; init; } = [];
    public SampleRequestAdminSummaryRowDto Totals { get; init; } = new();
}

public sealed class SampleRequestAdminSummaryRowDto
{
    public Guid? BranchId { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchName { get; init; }
    public int ReceivedCount { get; init; }
    public int CompletedCount { get; init; }
    public int OpenCount { get; init; }
    public int CancelledCount { get; init; }
    public int UnknownTerminalDateCount { get; init; }
}
