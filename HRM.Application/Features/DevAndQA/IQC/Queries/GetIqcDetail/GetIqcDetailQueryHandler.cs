using HRM.Application.Commons.Authorization;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services;
using MediatR;

namespace HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcDetail;

/// <summary>Lấy context dòng phiếu và QC mới nhất; trả context tạo mới khi dòng chưa có QC.</summary>
internal sealed class GetIqcDetailQueryHandler(IIqcDbContext context, ICurrentUser currentUser, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetIqcDetailQuery, OperationResult<GetQCInputByQC?>>
{
    public async Task<OperationResult<GetQCInputByQC?>> Handle(GetIqcDetailQuery query, CancellationToken ct)
    {
        var isIms = ImsAccountRestrictions.AppliesTo(currentUser);
        if (!isIms && !permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewIqc))
            return OperationResult<GetQCInputByQC?>.Fail("Bạn không có quyền xem IQC.");

        if (IqcRules.Company(currentUser) is not { } companyId)
            return OperationResult<GetQCInputByQC?>.Fail("Không lấy được công ty hiện tại.");

        if (query.VoucherDetailId <= 0)
            return OperationResult<GetQCInputByQC?>.Fail("VoucherDetailId không hợp lệ.");

        return OperationResult<GetQCInputByQC?>.Ok(
            await new IqcReader(context, companyId).GetDetailByVoucherDetailIdAsync(query.VoucherDetailId, ct, isIms));
    }
}
