namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    public sealed class ExecutivePnLAnalysisLineDto
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsBold { get; set; }
        public bool IsSubtotal { get; set; }
        public Dictionary<string, Dictionary<string, decimal>> MetricValues { get; set; } = new();
    }
}

