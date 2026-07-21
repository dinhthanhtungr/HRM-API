namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    /// <summary>
    /// Dai dien cho mot bieu do trong dashboard Executive PnL.
    /// Với Series dùng cho chart theo dãy số
    /// Với Points dùng cho scatter/bubble
    /// Categories giu label don gian de tuong thich FE cu; CategoryItems giu metadata day du cho chart nang cao.
    /// </summary>
    public sealed class ExecutivePnLChartDto
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<string> Categories { get; set; } = new();
        public List<ExecutivePnLChartCategoryDto> CategoryItems { get; set; } = new();
        public List<ExecutivePnLChartSeriesDto> Series { get; set; } = new();
        public List<ExecutivePnLChartPointDto> Points { get; set; } = new();
    }
}
