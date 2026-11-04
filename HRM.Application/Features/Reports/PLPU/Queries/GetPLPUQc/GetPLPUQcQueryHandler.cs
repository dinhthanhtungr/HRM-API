using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUQc;
internal sealed class GetPLPUQcQueryHandler(PLPUPurchaseOverviewReportService service, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPUQcQuery, OperationResult<PLPUPagedResult<PLPUPurchaseQcReportDto>>>
{
    public async Task<OperationResult<PLPUPagedResult<PLPUPurchaseQcReportDto>>> Handle(GetPLPUQcQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<PLPUPagedResult<PLPUPurchaseQcReportDto>>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<PLPUPagedResult<PLPUPurchaseQcReportDto>>.Fail(error);
        request.CompanyId = user.CompanyId;
        var data = await service.GetQcAsync(request, ct);
        return OperationResult<PLPUPagedResult<PLPUPurchaseQcReportDto>>.Ok(data);
    }
}
