using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUOrderDetail;
internal sealed class GetPLPUOrderDetailQueryHandler(PLPUPurchaseOverviewReportService service, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPUOrderDetailQuery, OperationResult<PLPUPurchaseOrderDetailDashboardDto>>
{
    public async Task<OperationResult<PLPUPurchaseOrderDetailDashboardDto>> Handle(GetPLPUOrderDetailQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<PLPUPurchaseOrderDetailDashboardDto>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<PLPUPurchaseOrderDetailDashboardDto>.Fail(error);
        if (request.Id == Guid.Empty) return OperationResult<PLPUPurchaseOrderDetailDashboardDto>.Fail("Id must not be empty.");
        request.CompanyId = user.CompanyId;
        var data = await service.GetOrderDetailAsync(request.Id, user.CompanyId!.Value, ct);
        return OperationResult<PLPUPurchaseOrderDetailDashboardDto>.Ok(data);
    }
}
