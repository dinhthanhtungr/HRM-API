namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    public sealed class ExecutivePnLAnalysisMetricDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string ValueType { get; set; } = "Money";
    }
}

