using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Reporting;
using HRM.Application.Features.Reports.Sales.Queries.GetSalesReportHeader;
using MediatR;

namespace HRM.Application.Features.Reports.Sales.Queries.GetSalesReportBody;

internal sealed class GetSalesReportBodyQueryHandler(IReportReadDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IDateTimeProvider clock)
    : IRequestHandler<GetSalesReportBodyQuery, OperationResult<SalesReportBodyDto>>
{
    public async Task<OperationResult<SalesReportBodyDto>> Handle(GetSalesReportBodyQuery request,
        CancellationToken ct)
    {
        if (!ReportCompanyScope.IsAllowed(user, request.CompanyId)
            || !user.IsInAnyRole(ApplicationRoleSets.Reports.SalesDashboardReaders))
            throw new UnauthorizedAccessException("Sales analysis is outside the current user scope.");
        var period = SalesReportPeriod.Create(request.From, request.To, clock.Now.Date);
        if (period is null || !Enum.IsDefined(request.GroupBy)
            || request.SaleGroup == Guid.Empty || request.SalePerson == Guid.Empty)
            return OperationResult<SalesReportBodyDto>.Fail("Khoảng ngày hoặc chiều phân tích không hợp lệ.");
        var canViewProfit = permissions.HasPermission(ApplicationPermissions.Reports.ViewSalesProfit);
        var rows = await new SalesReportBodyReader(db, user).ReadAsync(request, period,
            user.CompanyId!.Value, canViewProfit, ct);
        return OperationResult<SalesReportBodyDto>.Ok(SalesReportBodyBuilder.Build(
            user.CompanyId.Value, period, request.GroupBy, canViewProfit, rows));
    }
}
