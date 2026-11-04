using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Reporting;
using MediatR;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;

internal sealed class GetSalesReportHeaderQueryHandler(
    IReportReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions, IDateTimeProvider clock)
    : IRequestHandler<GetSalesReportHeaderQuery, OperationResult<SalesReportHeaderDto>>
{
    public async Task<OperationResult<SalesReportHeaderDto>> Handle(
        GetSalesReportHeaderQuery request, CancellationToken cancellationToken)
    {
        if (!ReportCompanyScope.IsAllowed(user, request.CompanyId)
            || !user.IsInAnyRole(ApplicationRoleSets.Reports.SalesScopedReaders))
            return OperationResult<SalesReportHeaderDto>.Fail("Không có quyền xem công ty này.");

        var today = clock.Now.Date;
        var period = SalesReportPeriod.Create(request.From, request.To, today);

        if (period is null || request.SaleGroup == Guid.Empty || request.SalePerson == Guid.Empty)
            return OperationResult<SalesReportHeaderDto>.Fail("Khoảng ngày hoặc bộ lọc sale không hợp lệ (tối đa 1096 ngày).");

        var reader = new SalesReportHeaderReader(db, user);
        var result = await reader.ReadAsync(request, period, user.CompanyId!.Value,
            permissions.HasPermission(ApplicationPermissions.Reports.ViewSalesProfit), today, cancellationToken);

        return OperationResult<SalesReportHeaderDto>.Ok(result);
    }
}
