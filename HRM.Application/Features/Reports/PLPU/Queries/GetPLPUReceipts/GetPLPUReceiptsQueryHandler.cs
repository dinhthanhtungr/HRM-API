using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUReceipts;
internal sealed class GetPLPUReceiptsQueryHandler(PLPUPurchaseOverviewReportService service, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPUReceiptsQuery, OperationResult<PLPUPagedResult<PLPUPurchaseReceiptReportDto>>>
{
    public async Task<OperationResult<PLPUPagedResult<PLPUPurchaseReceiptReportDto>>> Handle(GetPLPUReceiptsQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<PLPUPagedResult<PLPUPurchaseReceiptReportDto>>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<PLPUPagedResult<PLPUPurchaseReceiptReportDto>>.Fail(error);
        request.CompanyId = user.CompanyId;
        var data = await service.GetReceiptsAsync(request, ct);
        return OperationResult<PLPUPagedResult<PLPUPurchaseReceiptReportDto>>.Ok(data);
    }
}
