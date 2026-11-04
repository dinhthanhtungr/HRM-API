using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUHeader;
internal sealed class GetPLPUHeaderQueryHandler(PLPUPurchaseOverviewReportService service, ICurrentUser user, ICurrentUserPermissionService permissions, PLPUAccountingHeaderReader accounting)
    : IRequestHandler<GetPLPUHeaderQuery, OperationResult<PLPUPurchaseOverviewSummaryDto>>
{
    public async Task<OperationResult<PLPUPurchaseOverviewSummaryDto>> Handle(GetPLPUHeaderQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<PLPUPurchaseOverviewSummaryDto>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<PLPUPurchaseOverviewSummaryDto>.Fail(error);
        request.CompanyId = user.CompanyId;
        var data = await service.GetSummaryAsync(request, ct);
        data.Accounting = await accounting.ReadAsync(request, ct);
        return OperationResult<PLPUPurchaseOverviewSummaryDto>.Ok(data);
    }
}
