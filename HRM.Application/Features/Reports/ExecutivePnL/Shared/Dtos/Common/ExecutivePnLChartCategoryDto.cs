namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    /// <summary>
    /// Metadata cua mot category trong chart, dung de FE biet label hien thi, key entity va group cha neu co.
    /// </summary>
    public sealed class ExecutivePnLChartCategoryDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? GroupKey { get; set; }
        public string? GroupLabel { get; set; }
    }
}
