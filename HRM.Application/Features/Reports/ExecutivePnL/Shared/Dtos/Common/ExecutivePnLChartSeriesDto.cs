using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    /// <summary>
    /// Ð?i di?n m?t series trong chart
    /// </summary>
    public sealed class ExecutivePnLChartSeriesDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "bar";
        public List<decimal> Data { get; set; } = new();
    }
}

