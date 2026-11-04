using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUOrders;
internal sealed class GetPLPUOrdersQueryHandler(PLPUPurchaseOverviewReportService service, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPUOrdersQuery, OperationResult<PLPUPagedResult<PLPUPurchaseOrderRowDto>>>
{
    public async Task<OperationResult<PLPUPagedResult<PLPUPurchaseOrderRowDto>>> Handle(GetPLPUOrdersQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<PLPUPagedResult<PLPUPurchaseOrderRowDto>>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<PLPUPagedResult<PLPUPurchaseOrderRowDto>>.Fail(error);
        request.CompanyId = user.CompanyId;
        var data = await service.GetRowsAsync(request, ct);
        return OperationResult<PLPUPagedResult<PLPUPurchaseOrderRowDto>>.Ok(data);
    }
}
