using HRM.Domain.Enums.WareHouses;
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
        if (!currentUser.IsAuthenticated || !PurchaseOrderAccess.CanManage(permissions))
            return OperationResult.Fail("Bạn không có quyền xóa đơn mua hàng.");
        if (!currentUser.CompanyId.HasValue || !currentUser.EmployeeId.HasValue || currentUser.CompanyId == Guid.Empty || currentUser.EmployeeId == Guid.Empty)
            return OperationResult.Fail("Không xác định được công ty hoặc nhân viên hiện tại.");

        if (command.Notes is not null && PurchaseOrderNoteRules.Validate(command.Notes) is { } error)
            return OperationResult.Fail(error);

        if (!await dbContext.Employees.AnyAsync(x => x.EmployeeId == currentUser.EmployeeId.Value && x.CompanyId == currentUser.CompanyId.Value && x.IsActive, cancellationToken))
            return OperationResult.Fail("Nhân viên không thuộc công ty hiện tại.");
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await dbContext.LockPurchaseOrderAsync(command.PurchaseOrderId, currentUser.CompanyId.Value, cancellationToken);
        var purchaseOrder = await dbContext.PurchaseOrders.FirstOrDefaultAsync(item =>
            item.PurchaseOrderId == command.PurchaseOrderId &&
            item.CompanyId == currentUser.CompanyId.Value &&
            item.IsActive == true,
            cancellationToken);
        if (purchaseOrder is null)
            return OperationResult.Fail("Không tìm thấy đơn mua hàng.");
        if (!PurchaseOrderEditRules.CanEdit(purchaseOrder)) return OperationResult.Fail(PurchaseOrderEditRules.LockedMessage);
        await dbContext.PurchaseOrderLinks.Where(x => x.PurchaseOrderId == purchaseOrder.PurchaseOrderId && x.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        await dbContext.PurchaseOrderDetails.Where(x => x.PurchaseOrderId == purchaseOrder.PurchaseOrderId && x.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        if (!string.IsNullOrWhiteSpace(purchaseOrder.ExternalId))
        {
            var code = purchaseOrder.ExternalId.Trim();
            await dbContext.WarehouseRequests.Where(x => x.CompanyId == currentUser.CompanyId.Value && x.codeFromRequest == code && x.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.ReqStatus, WarehouseRequestStatus.Cancelled)
                    .SetProperty(x => x.UpdatedDate, dateTimeProvider.Now)
                    .SetProperty(x => x.UpdatedBy, currentUser.EmployeeId.Value), cancellationToken);
        }
        if (command.Notes is not null) PurchaseOrderNoteRules.Apply(purchaseOrder, command.Notes);
        purchaseOrder.IsActive = false;
        purchaseOrder.UpdatedBy = currentUser.EmployeeId.Value;
        purchaseOrder.UpdatedDate = dateTimeProvider.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("Đã xóa đơn mua hàng.");
    }
}
