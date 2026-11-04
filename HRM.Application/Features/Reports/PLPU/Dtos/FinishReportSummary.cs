using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.PLPU
{
    public class FinishReportSummary
    {
        public int TotalOrders { get; set; }
        public int OnTimeOrders { get; set; }
        public int LateOrders { get; set; }

        public decimal OnTimePercent { get; set; }
        public decimal LatePercent { get; set; }
    }
}
