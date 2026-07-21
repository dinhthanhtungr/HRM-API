using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models
{
    internal sealed class ExecutivePnLRawMonthlyData
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
    }
}

