using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using MediatR;

namespace HRM.Application.Features.Reports.CompanyOverview;

internal sealed class GetCompanyOverviewQueryHandler(
    ICurrentUser user, ICurrentUserPermissionService permissions, IDateTimeProvider clock,
    PLPUPurchaseOverviewReportService rawMaterials, ICompanyOverviewReader reader, ISender sender)
    : IRequestHandler<GetCompanyOverviewQuery, OperationResult<CompanyOverviewDto>>
{
    public async Task<OperationResult<CompanyOverviewDto>> Handle(GetCompanyOverviewQuery request, CancellationToken ct)
    {
        if (!CompanyOverviewAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<CompanyOverviewDto>.Fail("Access denied for current company or capability.");
        var period = SalesReportPeriod.Create(request.From, request.To, clock.Now.Date);
        if (period is null)
            return OperationResult<CompanyOverviewDto>.Fail("Invalid date range (maximum 1096 days).");
        var companyId = user.CompanyId!.Value;
        // Sequential: the existing readers share a scoped EF DbContext.
        var sales = await sender.Send(new GetSalesReportHeaderQuery
        {
            CompanyId = companyId, From = period.From, To = period.To
        }, ct);
        if (!sales.Success || sales.Data is null)
            return OperationResult<CompanyOverviewDto>.Fail(sales.Message ?? "Sales report is unavailable.");
        var trendFrom = new DateTime(period.To.Year, period.To.Month, 1).AddMonths(-5);
        var nvl = await rawMaterials.GetSummaryWithMonthlyAsync(companyId, period.From, period.To, trendFrom, ct);
        var purchases = await reader.ReadPurchasesAsync(companyId,
            period.From < trendFrom ? period.From : trendFrom, period.ToExclusive, ct);
        var stocks = await reader.ReadCurrentStockAsync(companyId, ct);
        return OperationResult<CompanyOverviewDto>.Ok(new CompanyOverviewDto
        {
            CompanyId = companyId, From = period.From, To = period.To, UpdatedAt = clock.Now,
            RawMaterials = nvl.Summary, Sales = sales.Data,
            Purchases = CompanyPurchaseCalculator.Build(purchases.Where(x => x.Date >= period.From && x.Date < period.ToExclusive).ToArray()),
            MonthlyPurchases = CompanyPurchaseCalculator.BuildMonthly(trendFrom, period.To, nvl.Monthly, purchases),
            CurrentStock = stocks
        });
    }
}
