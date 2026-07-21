using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    /// <summary>
    /// Ch?a meta data danh sách tab
    /// </summary>
    public sealed class ExecutivePnLDashboardDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string BusinessUnit { get; set; } = "MASTER BATCH";
        public string Currency { get; set; } = "VND";
        public DateTime FromMonth { get; set; }
        public DateTime ToMonth { get; set; }

        public List<ExecutivePnLDashboardTabDto> Tabs { get; set; } = new();
    }
}

