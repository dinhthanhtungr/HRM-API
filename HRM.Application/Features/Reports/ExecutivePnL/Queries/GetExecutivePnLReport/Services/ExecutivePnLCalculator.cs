using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services
{
    internal static class ExecutivePnLCalculator
    {
        public static decimal TotalCost(ExecutivePnLMonthlyActual actual)
        {
            return actual.CostOfSales + actual.ElectricityCost + actual.FreightAmount;
        }

        public static decimal GrossMargin(ExecutivePnLMonthlyActual actual)
        {
            return actual.NetSales - actual.CostOfSales - actual.ElectricityCost;
        }

        public static decimal GrossMarginPercent(ExecutivePnLMonthlyActual actual)
        {
            if (actual.NetSales == 0)
            {
                return 0;
            }

            return GrossMargin(actual) / actual.NetSales * 100;
        }
    }
}

