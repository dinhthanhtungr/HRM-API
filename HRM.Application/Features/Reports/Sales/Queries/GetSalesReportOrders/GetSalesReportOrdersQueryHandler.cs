using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using HRM.Application.Features.Reports.Sales.Shared;
using MediatR;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportOrders;

internal sealed class GetSalesReportOrdersQueryHandler(
    IReportReadDbContext db, ICurrentUser user, IDateTimeProvider clock)
    : IRequestHandler<GetSalesReportOrdersQuery, OperationResult<SalesReportDrawerPage<SalesReportOrderDto>>>
{
    public async Task<OperationResult<SalesReportDrawerPage<SalesReportOrderDto>>> Handle(
        GetSalesReportOrdersQuery request, CancellationToken ct)
    {
        if (!ReportCompanyScope.IsAllowed(user, request.CompanyId))
            return OperationResult<SalesReportDrawerPage<SalesReportOrderDto>>.Fail("Không có quyền xem công ty này.");
        var today = clock.Now.Date;
        var period = SalesReportPeriod.Create(request.From, request.To, today);
        if (period is null || !request.IsValid() || !SalesReportOrdersReader.IsValidMetric(request.Metric))
            return OperationResult<SalesReportDrawerPage<SalesReportOrderDto>>.Fail("Kỳ ngày, metric hoặc phân trang không hợp lệ.");
        var result = await new SalesReportOrdersReader(db, user).ReadAsync(request, period, today, ct);
        return OperationResult<SalesReportDrawerPage<SalesReportOrderDto>>.Ok(result);
    }
}
