using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;
using HRM.Domain.Entities.DeliverySchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.PatchDeliveryOrder;

/// <summary>Sửa ghi chú, phí, người giao và số bao; không thay quantity/lot đã lưu; đồng bộ trạng thái DHG.</summary>
internal sealed class PatchDeliveryOrderCommandHandler(IDispatchWriteDbContext db, ICurrentUser user, DeliveryWarehouseRequestService warehouseRequests, DeliveryOrderFulfillmentService fulfillment)
    : IRequestHandler<PatchDeliveryOrderCommand, OperationResult>
{
    public async Task<OperationResult> Handle(PatchDeliveryOrderCommand request, CancellationToken ct)
    {
        if (!DeliveryOrderAccessRules.CanManage(user) || user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            user.EmployeeId is not { } employeeId || employeeId == Guid.Empty || request.Id == Guid.Empty)
            return OperationResult.Fail("Bạn không có quyền hoặc thiếu thông tin phiếu/công ty/nhân viên.");
        if (request.Note?.Length > 2000 || request.DeliveryPrice < 0 ||
            request.Details?.Any(x => x.Id == Guid.Empty || x.NumOfBags < 0) == true ||
            request.Details?.GroupBy(x => x.Id).Any(g => g.Count() > 1) == true)
            return OperationResult.Fail("Thông tin cập nhật không hợp lệ.");

        await using var transaction = await db.BeginDeliveryOrderTransactionAsync(ct);
        // Khóa header cùng các thao tác in/xóa, tránh ghi lại record sau khi vừa bị hủy.
        var now = DateTime.Now;
        var locked = await db.DeliveryOrders.Where(x => x.Id == request.Id && x.CompanyId == companyId && x.IsActive &&
                x.Status != "Canceled" && x.Status != "Cancelled")
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedBy, employeeId).SetProperty(x => x.UpdatedDate, now), ct);
        if (locked == 0) return OperationResult.Fail("Không tìm thấy phiếu giao hợp lệ.");
        var order = await db.DeliveryOrders.SingleAsync(x => x.Id == request.Id && x.CompanyId == companyId, ct);
        var ids = request.Details?.Select(x => x.Id).ToArray() ?? [];
        var details = await db.DeliveryOrderDetails.Where(x => x.DeliveryOrderId == order.Id && x.IsActive && ids.Contains(x.Id)).ToListAsync(ct);
        if (details.Count != ids.Length) return OperationResult.Fail("Có chi tiết không thuộc phiếu giao này.");
        if (request.DelivererInforIds is not null)
        {
            var incoming = request.DelivererInforIds.Where(x => x != Guid.Empty).Distinct().ToArray();
            if (await db.DelivererInfors.CountAsync(x => incoming.Contains(x.Id) && x.IsActive, ct) != incoming.Length)
                return OperationResult.Fail("Có người giao hàng không hợp lệ.");
            var existing = await db.Deliverers.Where(x => x.DeliveryOrderId == order.Id).ToListAsync(ct);
            db.Deliverers.RemoveRange(existing.Where(x => !incoming.Contains(x.DelivererInforId)));
            foreach (var id in incoming.Except(existing.Select(x => x.DelivererInforId)))
                db.Deliverers.Add(new Deliverer { Id = Guid.CreateVersion7(), DeliveryOrderId = order.Id, DelivererInforId = id });
        }
        if (request.Note is not null) order.Note = request.Note;
        if (request.DeliveryPrice.HasValue) order.DeliveryPrice = request.DeliveryPrice;
        foreach (var detail in details) detail.NumOfBags = request.Details!.Single(x => x.Id == detail.Id).NumOfBags;
        var bagError = await warehouseRequests.PatchLotBagsAsync(order, request.LotBags, ids, ct);
        if (bagError is not null) return OperationResult.Fail(bagError);
        await db.SaveChangesAsync(ct);
        await fulfillment.SyncAsync(order.Id, companyId, employeeId, ct);
        await transaction.CommitAsync(ct);
        return OperationResult.Ok("Cập nhật phiếu giao hàng thành công.");
    }
}
