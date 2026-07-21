namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    public sealed class ExecutivePnLAnalysisTabDto
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        public List<ExecutivePnLChartDto> Charts { get; set; } = new();
        public List<ExecutivePnLColumnDto> Columns { get; set; } = new();
        public List<ExecutivePnLAnalysisMetricDto> Metrics { get; set; } = new();
        public List<ExecutivePnLAnalysisSectionDto> Sections { get; set; } = new();
    }
}

