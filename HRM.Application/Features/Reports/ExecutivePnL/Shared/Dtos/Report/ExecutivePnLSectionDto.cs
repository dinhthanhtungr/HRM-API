using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    public sealed class ExecutivePnLSectionDto
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public List<ExecutivePnLLineDto> Lines { get; set; } = new();
    }
}

