using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using HRM.Application.Features.Reports.Sales.Shared;
using MediatR;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportCustomers;

internal sealed class GetSalesReportCustomersQueryHandler(
    IReportReadDbContext db, ICurrentUser user, IDateTimeProvider clock)
    : IRequestHandler<GetSalesReportCustomersQuery, OperationResult<SalesReportDrawerPage<SalesReportCustomerDto>>>
{
    public async Task<OperationResult<SalesReportDrawerPage<SalesReportCustomerDto>>> Handle(
        GetSalesReportCustomersQuery request, CancellationToken ct)
    {
        if (!ReportCompanyScope.IsAllowed(user, request.CompanyId))
            return OperationResult<SalesReportDrawerPage<SalesReportCustomerDto>>.Fail("Không có quyền xem công ty này.");
        var period = SalesReportPeriod.Create(request.From, request.To, clock.Now.Date);
        if (period is null || !request.IsValid() || request.Metric != "deliveredCustomer")
            return OperationResult<SalesReportDrawerPage<SalesReportCustomerDto>>.Fail("Kỳ ngày, metric hoặc phân trang không hợp lệ.");
        var result = await new SalesReportCustomersReader(db, user).ReadAsync(request, period, ct);
        return OperationResult<SalesReportDrawerPage<SalesReportCustomerDto>>.Ok(result);
    }
}
