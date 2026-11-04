using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
using HRM.Application.Abstractions.Commons.Time;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUBody;
internal sealed class GetPLPUBodyQueryHandler(PLPUPurchaseOverviewReportService service, ICurrentUser user, ICurrentUserPermissionService permissions, IDateTimeProvider clock)
    : IRequestHandler<GetPLPUBodyQuery, OperationResult<PLPUPurchaseBodyDto>>
{
    public async Task<OperationResult<PLPUPurchaseBodyDto>> Handle(GetPLPUBodyQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<PLPUPurchaseBodyDto>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<PLPUPurchaseBodyDto>.Fail(error);
        request.CompanyId = user.CompanyId;
        if (!request.To.HasValue && request.From?.Date > clock.Now.Date)
            return OperationResult<PLPUPurchaseBodyDto>.Fail("From must not exceed today when To is omitted.");
        var window = PLPUMonthChartWindow.Resolve(request, clock.Now.Date);
        if (window.To == DateTime.MaxValue.Date)
            return OperationResult<PLPUPurchaseBodyDto>.Fail("Month is outside the supported date range.");
        var dashboard = await service.GetHeaderReportAsync(request, ct);
        var monthly = await service.GetHeaderReportAsync(
            PLPUMonthChartWindow.CreateQuery(request, window.From, window.To), ct);
        var data = new PLPUPurchaseBodyDto
        {
            PurchaseValueByMonth = PLPUMonthChartWindow.Fill(monthly.PurchaseValueByMonth, window.From, window.To),
            QuantityByMonth = PLPUMonthChartWindow.Fill(monthly.QuantityByMonth, window.From, window.To),
            PurchaseValueBySupplier = dashboard.PurchaseValueBySupplier,
            QuantityBySupplier = dashboard.QuantityBySupplier,
            QuantityByMaterial = dashboard.QuantityByMaterial,
            QcResultBySupplier = dashboard.QcResultBySupplier,
            StatusDistribution = dashboard.StatusDistribution,
            PriceVarianceByMaterial = dashboard.PriceVarianceByMaterial,
        };
        return OperationResult<PLPUPurchaseBodyDto>.Ok(data);
    }
}
