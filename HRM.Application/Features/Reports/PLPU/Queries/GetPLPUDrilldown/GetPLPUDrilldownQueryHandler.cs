using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUDrilldown;

internal sealed class GetPLPUDrilldownQueryHandler(
    PLPUPurchaseOverviewReportService service, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPUDrilldownQuery, OperationResult<PLPUDrilldownDto>>
{
    public async Task<OperationResult<PLPUDrilldownDto>> Handle(GetPLPUDrilldownQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<PLPUDrilldownDto>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<PLPUDrilldownDto>.Fail(error);
        if (!PLPUPurchaseOverviewReportService.IsDrilldownMetric(request.Metric))
            return OperationResult<PLPUDrilldownDto>.Fail("Unsupported PLPU drill-down metric.");
        var statusError = PLPUPurchaseOverviewReportService.ValidateReportStatus(request.Metric, request.ReportStatus);
        if (statusError != null) return OperationResult<PLPUDrilldownDto>.Fail(statusError);
        request.CompanyId = user.CompanyId;
        return OperationResult<PLPUDrilldownDto>.Ok(await service.GetDrilldownAsync(request, request.Metric!, ct, request.ReportStatus));
    }
}
