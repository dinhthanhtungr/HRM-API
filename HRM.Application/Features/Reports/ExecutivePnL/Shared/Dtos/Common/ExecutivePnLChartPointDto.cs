namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    /// <summary>
    /// Mot diem du lieu scatter/bubble chart.
    /// </summary>
    public sealed class ExecutivePnLChartPointDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? GroupKey { get; set; }
        public string? GroupLabel { get; set; }
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Z { get; set; }
    }
}
