namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

public sealed class SampleActivityCountsDto
{
    public int Completed { get; init; }
    public int Responses { get; init; }
    public int Accepted { get; init; }
    public int Rejected { get; init; }
    public int Waiting { get; init; }
    public int LabDeclined { get; init; }
    public int Cancelled { get; init; }
    public int Unknown { get; init; }
    public int Optional { get; init; }
}

public sealed class SampleActivityGroupDto
{
    public Guid? BranchId { get; init; }
    public string? BranchName { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string? Team { get; init; }
    public IReadOnlyDictionary<string, SampleActivityCountsDto> Counts { get; init; } = new Dictionary<string, SampleActivityCountsDto>();
}

public sealed class SampleActivityReportDto
{
    public DateTime ResultsAsOf { get; init; }
    public IReadOnlyDictionary<string, SampleActivityCountsDto> Totals { get; init; } = new Dictionary<string, SampleActivityCountsDto>();
    public IReadOnlyList<SampleActivityGroupDto> Branches { get; init; } = [];
    public IReadOnlyList<SampleActivityGroupDto> Employees { get; init; } = [];
}

public sealed class SampleActivityDetailDto
{
    public Guid ActivityId { get; init; }
    public Guid SampleRequestId { get; init; }
    public string RequestCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string? ColourCode { get; init; }
    public string? FormulaCode { get; init; }
    public int? TrialNo { get; init; }
    public string Kind { get; init; } = "";
    public DateTime CompletedAt { get; init; }
    public string? EmployeeName { get; init; }
    public string Outcome { get; init; } = "";
    public int Responses { get; init; }
    public DateTime? LatestResponseAt { get; init; }
}
