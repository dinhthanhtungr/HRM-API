using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.UpdatePurchaseOrderNotes;

internal sealed class UpdatePurchaseOrderNotesCommandHandler(
    IPurchaseOrderDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdatePurchaseOrderNotesCommand, OperationResult>
{
    private static readonly HashSet<string> AllowedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "comment",
        "plpuComment"
    };

    public async Task<OperationResult> Handle(UpdatePurchaseOrderNotesCommand command, CancellationToken cancellationToken)
    {
        if (!PurchaseOrderAccess.CanManage(permissions))
            return OperationResult.Fail("Bạn không có quyền cập nhật đơn mua hàng.");
        if (!currentUser.CompanyId.HasValue || !currentUser.EmployeeId.HasValue)
            return OperationResult.Fail("Không xác định được công ty hoặc nhân viên hiện tại.");

        var clearFields = command.Request.ClearFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (clearFields.Any(field => !AllowedFields.Contains(field)))
            return OperationResult.Fail("ClearFields chỉ hỗ trợ comment và plpuComment.");
        if (clearFields.Contains("comment") && command.Request.Comment is not null ||
            clearFields.Contains("plpuComment") && command.Request.PlpuComment is not null)
            return OperationResult.Fail("Một field không thể vừa có giá trị vừa nằm trong ClearFields.");
        if (command.Request.Comment is null && command.Request.PlpuComment is null && clearFields.Count == 0)
            return OperationResult.Fail("Không có nội dung cần cập nhật.");

        var purchaseOrder = await dbContext.PurchaseOrders.FirstOrDefaultAsync(item =>
            item.PurchaseOrderId == command.PurchaseOrderId &&
            item.CompanyId == currentUser.CompanyId.Value &&
            item.IsActive == true,
            cancellationToken);
        if (purchaseOrder is null)
            return OperationResult.Fail("Không tìm thấy đơn mua hàng.");
        if (string.Equals(purchaseOrder.Status, PurchaseOrderStatus.Canceled, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(purchaseOrder.Status, PurchaseOrderStatus.Completed, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Fail("Không thể sửa đơn mua hàng đã hủy hoặc hoàn tất.");

        if (clearFields.Contains("comment"))
            purchaseOrder.Comment = null;
        else if (command.Request.Comment is not null)
            purchaseOrder.Comment = command.Request.Comment.Trim();

        if (clearFields.Contains("plpuComment"))
            purchaseOrder.PLPUComment = null;
        else if (command.Request.PlpuComment is not null)
            purchaseOrder.PLPUComment = command.Request.PlpuComment.Trim();

        purchaseOrder.UpdatedBy = currentUser.EmployeeId.Value;
        purchaseOrder.UpdatedDate = dateTimeProvider.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Đã cập nhật ghi chú đơn mua hàng.");
    }
}
