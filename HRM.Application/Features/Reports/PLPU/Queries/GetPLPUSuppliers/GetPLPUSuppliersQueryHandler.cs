using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUSuppliers;
internal sealed class GetPLPUSuppliersQueryHandler(PLPUPurchaseOverviewReportService service, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPUSuppliersQuery, OperationResult<List<PLPUSupplierPurchasePerformanceDto>>>
{
    public async Task<OperationResult<List<PLPUSupplierPurchasePerformanceDto>>> Handle(GetPLPUSuppliersQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<List<PLPUSupplierPurchasePerformanceDto>>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<List<PLPUSupplierPurchasePerformanceDto>>.Fail(error);
        request.CompanyId = user.CompanyId;
        var data = await service.GetSupplierPerformanceAsync(request, ct);
        return OperationResult<List<PLPUSupplierPurchasePerformanceDto>>.Ok(data);
    }
}
