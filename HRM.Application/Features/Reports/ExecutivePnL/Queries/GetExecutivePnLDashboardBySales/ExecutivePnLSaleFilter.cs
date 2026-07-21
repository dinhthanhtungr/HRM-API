using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLDashboardBySales
{
    internal sealed class ExecutivePnLSaleFilter : ExecutivePnLFilter
    {
        public Guid? SaleGroup { get; init;  }
        public Guid? SalePerson { get; init; }
    }
}

