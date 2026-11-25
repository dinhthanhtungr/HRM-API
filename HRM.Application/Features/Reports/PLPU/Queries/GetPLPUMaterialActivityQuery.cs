using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.PLPU.Queries;

public sealed class GetPLPUMaterialActivityQuery : PLPUPurchaseOverviewQuery,
    IRequest<OperationResult<PLPUMaterialActivityDto>>
{
}

internal sealed class GetPLPUMaterialActivityQueryHandler(
    PLPUPurchaseOverviewReportService service, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPUMaterialActivityQuery, OperationResult<PLPUMaterialActivityDto>>
{
    public async Task<OperationResult<PLPUMaterialActivityDto>> Handle(GetPLPUMaterialActivityQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<PLPUMaterialActivityDto>.Fail("Access denied for current company or capability.");

        var error = PLPUReportAccess.Validate(request);
        if (error != null)
            return OperationResult<PLPUMaterialActivityDto>.Fail(error);
        if (!request.From.HasValue || !request.To.HasValue)
            return OperationResult<PLPUMaterialActivityDto>.Fail("From and To are required.");

        request.CompanyId = user.CompanyId;
        var activity = await service.GetMaterialActivityAsync(request, ct);
        return activity is null
            ? OperationResult<PLPUMaterialActivityDto>.Fail("Material activity is unavailable.")
            : OperationResult<PLPUMaterialActivityDto>.Ok(activity);
    }
}
