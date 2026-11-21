using HRM.Application.Commons.Authorization;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcSummary;

internal sealed class GetIqcSummaryQueryHandler(IIqcDbContext context, ICurrentUser currentUser, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetIqcSummaryQuery, OperationResult<IqcPagedResult<GetSummaryQCInput>>>
{
    public async Task<OperationResult<IqcPagedResult<GetSummaryQCInput>>> Handle(GetIqcSummaryQuery query, CancellationToken ct)
    {
        var isIms = ImsAccountRestrictions.AppliesTo(currentUser);
        if (!isIms && !permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewIqc))
            return OperationResult<IqcPagedResult<GetSummaryQCInput>>.Fail("Bạn không có quyền xem IQC.");

        if (IqcRules.Company(currentUser) is not { } companyId)
            return OperationResult<IqcPagedResult<GetSummaryQCInput>>.Fail("Không lấy được công ty hiện tại.");
        
        var error = IqcRules.Validate(query.Filter);
        
        if (error is not null) 
            return OperationResult<IqcPagedResult<GetSummaryQCInput>>.Fail(error);
        
        if (isIms) query.Filter.IncludeLabTests = false;

        var reader = new IqcReader(context, companyId);
        
        return OperationResult<IqcPagedResult<GetSummaryQCInput>>.Ok(
            await reader.GetPagedSummaryAsync(query.Filter, ct));
    }
}
