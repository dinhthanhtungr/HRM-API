using HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Builders
{
    internal sealed partial class ExecutivePnLDashboardBuilder
    {
        /// <summary>
        /// Dung mot series chart va lam tron du lieu ve 2 chu so thap phan.
        /// </summary>
        private static ExecutivePnLChartSeriesDto BuildSeries(
            string key,
            string name,
            string type,
            IEnumerable<decimal> values)
        {
            return new ExecutivePnLChartSeriesDto
            {
                Key = key,
                Name = name,
                Type = type,
                Data = values.Select(x => Math.Round(x, 2)).ToList()
            };
        }

        /// <summary>
        /// Dung category metadata don gian khi chart chi can key va label giong nhau.
        /// </summary>
        private static List<ExecutivePnLChartCategoryDto> BuildCategoryItems(
            IEnumerable<string> labels)
        {
            return labels
                .Select(x => new ExecutivePnLChartCategoryDto
                {
                    Key = x,
                    Label = x
                })
                .ToList();
        }
    }
}
