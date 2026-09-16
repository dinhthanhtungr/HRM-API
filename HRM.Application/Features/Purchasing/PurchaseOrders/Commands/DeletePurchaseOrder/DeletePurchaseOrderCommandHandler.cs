using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.DeletePurchaseOrder;

internal sealed class DeletePurchaseOrderCommandHandler(
    IPurchaseOrderDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<DeletePurchaseOrderCommand, OperationResult>
{
    public async Task<OperationResult> Handle(DeletePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        if (!PurchaseOrderAccess.CanManage(permissions))
            return OperationResult.Fail("Bạn không có quyền xóa đơn mua hàng.");
        if (!currentUser.CompanyId.HasValue || !currentUser.EmployeeId.HasValue)
            return OperationResult.Fail("Không xác định được công ty hoặc nhân viên hiện tại.");

        var purchaseOrder = await dbContext.PurchaseOrders.FirstOrDefaultAsync(item =>
            item.PurchaseOrderId == command.PurchaseOrderId &&
            item.CompanyId == currentUser.CompanyId.Value &&
            item.IsActive == true,
            cancellationToken);
        if (purchaseOrder is null)
            return OperationResult.Fail("Không tìm thấy đơn mua hàng.");
        if (!string.Equals(purchaseOrder.Status, PurchaseOrderStatus.Pending, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Fail("Chỉ PO đang chờ xử lý mới được xóa.");

        purchaseOrder.IsActive = false;
        purchaseOrder.UpdatedBy = currentUser.EmployeeId.Value;
        purchaseOrder.UpdatedDate = dateTimeProvider.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Đã xóa đơn mua hàng.");
    }
}
