using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Models;

namespace HRM.Application.Features.Reports.ExecutivePnL.Queries.GetExecutivePnLReport.Services;

internal sealed partial class ExecutivePnLActualReader
{
    private readonly IReportReadDbContext _dbContext;

    public ExecutivePnLActualReader(IReportReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Dictionary<string, ExecutivePnLMonthlyActual>> GetActualsAsync(
        ExecutivePnLFilter filter,
        IReadOnlyList<DateTime> months,
        CancellationToken cancellationToken)
    {
        var actuals = months.ToDictionary(
            x => ExecutivePnLPeriod.MonthKey(x.Year, x.Month),
            _ => new ExecutivePnLMonthlyActual());

        await AddSalesActualsAsync(actuals, filter, cancellationToken);
        //ApplySalesDeductions(actuals);
        await AddCostOfSalesAsync(actuals, filter, cancellationToken);
        await AddElectricityCostAsync(actuals, filter, cancellationToken);
        await AddOrderCountsAsync(actuals, filter, cancellationToken);
        await AddDeliveryQuantitiesAsync(actuals, filter, cancellationToken);
        await AddFreightAmountsAsync(actuals, filter, cancellationToken);
        await AddProductionActualsAsync(actuals, filter, cancellationToken);

        foreach (var actual in actuals.Values)
        {
            actual.NetSales = actual.SalesRevenue - actual.SalesDeductions;
        }

        return actuals;
    }
}

