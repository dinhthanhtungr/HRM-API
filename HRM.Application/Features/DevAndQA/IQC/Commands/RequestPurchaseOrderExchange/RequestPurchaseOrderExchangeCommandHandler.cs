using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.DevAndQA.IQC.Services;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace HRM.Application.Features.DevAndQA.IQC.Commands.RequestPurchaseOrderExchange;

/// <summary>Kiểm tra quyền/company/employee, resolve DDH từ phiếu kho và lưu trạng thái trong transaction.</summary>
internal sealed class RequestPurchaseOrderExchangeCommandHandler(
    IIqcDbContext context,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    KeyedMutationLock<string> mutationLock)
    : IRequestHandler<RequestPurchaseOrderExchangeCommand, PurchaseOrderExchangeDto>
{
    public async Task<PurchaseOrderExchangeDto> Handle(RequestPurchaseOrderExchangeCommand request, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageIqc))
            throw new EvaluationRequestException("Bạn không có quyền sửa IQC.", 403);

        if (IqcRules.Company(user) is not { } companyId ||
            user.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            throw new EvaluationRequestException("Không lấy được công ty hoặc nhân viên hiện tại.", 403);

        if (request.VoucherDetailId <= 0)
            throw new EvaluationRequestException("VoucherDetailId không hợp lệ.");

        if (!await context.Employees.AnyAsync(x => x.EmployeeId == employeeId && x.CompanyId == companyId, ct))
            throw new EvaluationRequestException("Nhân viên không thuộc công ty hiện tại.", 403);

        using var guard = await mutationLock.AcquireAsync($"iqc:{companyId}", ct);
        await using var transaction = await context.BeginIqcTransactionAsync(ct);

        var detail = await context.WarehouseVoucherDetails.FirstOrDefaultAsync(
            x => x.VoucherDetailId == request.VoucherDetailId && x.Voucher.CompanyId == companyId, ct)
            ?? throw new EvaluationRequestException("Không tìm thấy dòng phiếu kho.", 404);

        var po = await new IqcPurchaseOrderSync(context).FindRelatedAsync(detail, companyId, ct)
            ?? throw new EvaluationRequestException("Không tìm thấy DDH liên quan.", 404);

        if (string.Equals(po.Status, PurchaseOrderStatus.Canceled, StringComparison.OrdinalIgnoreCase))
            throw new EvaluationRequestException("DDH đã hủy không thể yêu cầu đổi hàng.", 409);

        if (!string.Equals(po.Status, PurchaseOrderStatus.ExchangeRequested, StringComparison.OrdinalIgnoreCase))
        {
            po.Status = PurchaseOrderStatus.ExchangeRequested;
            po.UpdatedBy = employeeId;
            po.UpdatedDate = DateTime.Now;
            await context.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return new(po.PurchaseOrderId, po.ExternalId, PurchaseOrderStatus.ExchangeRequested);
    }
}
