using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUFinish;
internal sealed class GetPLPUFinishQueryHandler(FinishPLPUReportRepository finish, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPUFinishQuery, OperationResult<List<FinishRow>>>
{
    public async Task<OperationResult<List<FinishRow>>> Handle(GetPLPUFinishQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<List<FinishRow>>.Fail("Access denied for current company or capability.");
        var error = PLPUReportAccess.Validate(request);
        if (error != null) return OperationResult<List<FinishRow>>.Fail(error);
        if (!request.From.HasValue || !request.To.HasValue) return OperationResult<List<FinishRow>>.Fail("From and To are required.");
        request.CompanyId = user.CompanyId;
        var data = await finish.GetFinishReportAsync(request.From!.Value, request.To!.Value, user.CompanyId!.Value, ct);
        return OperationResult<List<FinishRow>>.Ok(data);
    }
}
