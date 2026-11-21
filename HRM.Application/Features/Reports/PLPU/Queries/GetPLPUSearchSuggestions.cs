using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Searching;
using MediatR;

namespace HRM.Application.Features.Reports.PLPU.Queries;

public sealed class GetPLPUSearchSuggestionsQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<IReadOnlyList<SmartSearchSuggestion>>>;

internal sealed class GetPLPUSearchSuggestionsHandler(IPurchaseOrderDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPLPUSearchSuggestionsQuery, OperationResult<IReadOnlyList<SmartSearchSuggestion>>>
{
    public async Task<OperationResult<IReadOnlyList<SmartSearchSuggestion>>> Handle(GetPLPUSearchSuggestionsQuery request, CancellationToken ct)
    {
        if (!PLPUReportAccess.CanRead(user, permissions, request.CompanyId))
            return OperationResult<IReadOnlyList<SmartSearchSuggestion>>.Fail("Access denied for current company or capability.");
        if (PLPUReportAccess.Validate(request) is {} error) return OperationResult<IReadOnlyList<SmartSearchSuggestion>>.Fail(error);
        request.CompanyId = user.CompanyId;
        return OperationResult<IReadOnlyList<SmartSearchSuggestion>>.Ok(await new PLPUPurchaseOverviewReportRepository(db).SuggestAsync(request, ct));
    }
}
