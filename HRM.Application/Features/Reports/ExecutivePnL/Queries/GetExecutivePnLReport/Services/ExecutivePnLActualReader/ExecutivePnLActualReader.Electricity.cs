using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;

internal sealed partial class ExecutivePnLActualReader
{
    private async Task AddElectricityCostAsync(
        Dictionary<string, ExecutivePnLMonthlyActual> actuals,
        ExecutivePnLFilter filter,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.ElectricityMonthlyBillRows
            .AsNoTracking()
            .Where(x => x.GroupCode == "TOTAL")
            .Where(x => x.Ym >= filter.FromMonth.Date && x.Ym < filter.RangeEnd.Date)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (!TryGetActual(actuals, row.Ym.Year, row.Ym.Month, out var actual))
            {
                continue;
            }

            actual.ElectricityCost += row.EnergyCostVnd;
        }
    }
}

