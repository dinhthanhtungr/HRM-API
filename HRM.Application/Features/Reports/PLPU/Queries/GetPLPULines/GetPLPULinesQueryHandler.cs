using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPULines;
internal sealed class GetPLPULinesQueryHandler(PLPUPurchaseOverviewReportService service, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPULinesQuery, OperationResult<PLPUPagedResult<PLPUPurchaseOrderDetailReportDto>>>
{
    public async Task<OperationResult<PLPUPagedResult<PLPUPurchaseOrderDetailReportDto>>> Handle(GetPLPULinesQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<PLPUPagedResult<PLPUPurchaseOrderDetailReportDto>>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<PLPUPagedResult<PLPUPurchaseOrderDetailReportDto>>.Fail(error);
        request.CompanyId = user.CompanyId;
        var data = await service.GetDetailLinesAsync(request, ct);
        return OperationResult<PLPUPagedResult<PLPUPurchaseOrderDetailReportDto>>.Ok(data);
    }
}
