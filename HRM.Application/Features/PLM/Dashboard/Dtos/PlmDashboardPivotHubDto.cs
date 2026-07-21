namespace HRM.Application.Features.PLM.Dashboard.Dtos;

public sealed class PlmDashboardPivotHubDto
{
    public IReadOnlyList<PlmDashboardPivotMetricDto> Metrics { get; set; } = [];
    public IReadOnlyList<PlmDashboardPivotTableDto> Tables { get; set; } = [];
}

public sealed class PlmDashboardPivotMetricDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Finished { get; set; }
    public decimal FinishRate { get; set; }
}

public sealed class PlmDashboardPivotTableDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IReadOnlyList<PlmDashboardPivotColumnDto> Columns { get; set; } = [];
    public IReadOnlyList<PlmDashboardPivotRowDto> Rows { get; set; } = [];
}

public sealed class PlmDashboardPivotColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class PlmDashboardPivotRowDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Finished { get; set; }
    public decimal FinishRate { get; set; }
    public Dictionary<string, PlmDashboardPivotCellDto> Cells { get; set; } = [];
}

public sealed class PlmDashboardPivotCellDto
{
    public int Total { get; set; }
    public int Finished { get; set; }
    public decimal FinishRate { get; set; }
}
