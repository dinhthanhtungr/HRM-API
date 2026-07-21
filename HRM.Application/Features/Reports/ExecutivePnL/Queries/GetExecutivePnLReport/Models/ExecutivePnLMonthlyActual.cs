using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models
{
    internal sealed class ExecutivePnLMonthlyActual
    {
        public decimal SalesTonnes { get; set; }
        public decimal SalesRevenue { get; set; }
        public decimal SalesDeductions { get; set; }
        public decimal NetSales { get; set; }
        public decimal CostOfSales { get; set; }
        public decimal ElectricityCost { get; set; }
        public decimal FreightAmount { get; set; }
        public decimal DeliveredQuantity { get; set; }
        public decimal ProductionQuantity { get; set; }
        public int OrderCount { get; set; }
        public int OrderLineCount { get; set; }
        public int ProductionOrderCount { get; set; }
    }
}

