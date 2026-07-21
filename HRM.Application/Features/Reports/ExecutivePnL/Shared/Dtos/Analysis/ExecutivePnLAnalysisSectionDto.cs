namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    public sealed class ExecutivePnLAnalysisSectionDto
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public List<ExecutivePnLAnalysisLineDto> Lines { get; set; } = new();
    }
}

