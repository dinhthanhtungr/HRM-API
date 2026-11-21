namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

public sealed class SampleRequestAdminSummaryDto
{
    public SampleActivityReportDto? Activities { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public string GroupSource => "SampleRequestBranch";
    public bool HasCompleteHistoricalStatus => false;
    public IReadOnlyList<SampleRequestAdminSummaryRowDto> Rows { get; init; } = [];
    public SampleRequestAdminSummaryRowDto Totals { get; init; } = new();
    public IReadOnlyList<SampleRequestCreatorSummaryRowDto> CreatorRows { get; init; } = [];
}

public sealed class SampleRequestCreatorSummaryRowDto
{
    public IReadOnlyDictionary<string, int> CompletedBreakdown { get; init; } = new Dictionary<string, int>();
    public string Team { get; init; } = string.Empty;
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public int CompletedCount { get; init; }
    public int InProgressCount { get; init; }
}

public sealed class SampleRequestAdminSummaryRowDto
{
    public IReadOnlyDictionary<string, int> CompletedBreakdown { get; init; } = new Dictionary<string, int>();
    public int PriceAcceptedCount { get; init; }
    public int ChipAcceptedCount { get; init; }
    public int SampleAcceptedCount { get; init; }
    public Guid? BranchId { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchName { get; init; }
    public int ReceivedCount { get; init; }
    public int CompletedCount { get; init; }
    public int OpenCount { get; init; }
    public int InProgressCount { get; init; }
    public int PendingCount { get; init; }
    public int LabDeclinedCount { get; init; }
    public int CancelledCount { get; init; }
    public int UnknownTerminalDateCount { get; init; }
}
