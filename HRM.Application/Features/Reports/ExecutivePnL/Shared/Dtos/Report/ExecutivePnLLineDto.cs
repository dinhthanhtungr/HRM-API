using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Dtos
{
    public sealed class ExecutivePnLLineDto
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsBold { get; set; }
        public bool IsSubtotal { get; set; }
        public Dictionary<string, decimal> Values { get; set; } = new();
    }
}

