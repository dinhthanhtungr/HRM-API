using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUFinishExport;
internal sealed class GetPLPUFinishExportQueryHandler(FinishPLPUReportRepository finish, IPLPUFinishReportExcelRenderer renderer, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPUFinishExportQuery, OperationResult<byte[]>>
{
    public async Task<OperationResult<byte[]>> Handle(GetPLPUFinishExportQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<byte[]>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<byte[]>.Fail(error);
        if (!request.From.HasValue || !request.To.HasValue) return OperationResult<byte[]>.Fail("From and To are required.");
        request.CompanyId = user.CompanyId;
        var rows = await finish.GetFinishReportAsync(request.From!.Value, request.To!.Value, user.CompanyId!.Value, ct);
        var data = renderer.ExportFinishReportExcel(rows);
        return OperationResult<byte[]>.Ok(data);
    }
}
