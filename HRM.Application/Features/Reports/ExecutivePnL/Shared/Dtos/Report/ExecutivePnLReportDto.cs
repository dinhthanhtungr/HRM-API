using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    public sealed class ExecutivePnLReportDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string BusinessUnit { get; set; } = "MASTER BATCH";
        public string Currency { get; set; } = "VND";
        public DateTime FromMonth { get; set; }
        public DateTime ToMonth { get; set; }

        public List<ExecutivePnLColumnDto> Columns { get; set; } = new();
        public List<ExecutivePnLSectionDto> Sections { get; set; } = new();
    }
}

