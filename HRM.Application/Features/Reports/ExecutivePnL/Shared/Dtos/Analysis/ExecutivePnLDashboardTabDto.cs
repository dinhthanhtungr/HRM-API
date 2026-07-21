using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    /// <summary>
    /// Ð?i di?n cho m?t tab
    /// M?i tab d?u có chart và table
    /// </summary>
    public sealed class ExecutivePnLDashboardTabDto
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        public List<ExecutivePnLChartDto> Charts { get; set; } = new();
        public List<ExecutivePnLColumnDto> Columns { get; set; } = new();
        public List<ExecutivePnLSectionDto> Sections { get; set; } = new();
    }
}

