using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Enums.WareHouses;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.DeleteDeliveryOrder;

/// <summary>Xóa mềm theo nghiệp vụ legacy, đồng bộ lượng đã ghi nhận khi lưu và hủy yêu cầu xuất kho.</summary>
internal sealed class DeleteDeliveryOrderCommandHandler(IDispatchWriteDbContext db, ICurrentUser user, DeliveryOrderFulfillmentService fulfillment)
    : IRequestHandler<DeleteDeliveryOrderCommand, OperationResult>
{
    public async Task<OperationResult> Handle(DeleteDeliveryOrderCommand request, CancellationToken ct)
    {
        if (!DeliveryOrderAccessRules.CanCancel(user) || user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            user.EmployeeId is not { } employeeId || employeeId == Guid.Empty || request.Id == Guid.Empty)
            return OperationResult.Fail("Bạn không có quyền hoặc thiếu thông tin phiếu/công ty/nhân viên.");
        await using var transaction = await db.BeginDeliveryOrderTransactionAsync(ct);
        var now = DateTime.Now;
        var locked = await db.DeliveryOrders.Where(x => x.Id == request.Id && x.CompanyId == companyId && x.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.Status, "Cancelled").SetProperty(x => x.UpdatedBy, employeeId)
                .SetProperty(x => x.UpdatedDate, now), ct);
        if (locked == 0)
        {
            if (!await db.DeliveryOrders.AnyAsync(x => x.Id == request.Id && x.CompanyId == companyId && !x.IsActive &&
                    (x.Status == "Cancelled" || x.Status == "Canceled"), ct))
                return OperationResult.Fail("Không tìm thấy phiếu giao hàng.");
            await transaction.CommitAsync(ct);
            return OperationResult.Ok("Phiếu giao đã được xóa trước đó.");
        }
        var order = await db.DeliveryOrders.AsNoTracking().SingleAsync(x => x.Id == request.Id && x.CompanyId == companyId, ct);
        // Header inactive loại phiếu khỏi phân bổ. Giữ cờ dòng/lot để bảo toàn snapshot cuối,
        // phân biệt với các phiên bản đã bị PUT vô hiệu hóa trước đó.
        if (!string.IsNullOrWhiteSpace(order.ExternalId))
        {
            var requests = await db.WarehouseRequests.Where(x => x.CompanyId == companyId && x.IsActive &&
                x.codeFromRequest == order.ExternalId && x.ReqType == WareHouseRequestType.ExportFinishedGood).ToListAsync(ct);
            var requestIds = requests.Select(x => x.RequestId).ToArray();
            foreach (var warehouseRequest in requests)
            {
                warehouseRequest.IsActive = false; warehouseRequest.ReqStatus = WarehouseRequestStatus.Cancelled;
                warehouseRequest.UpdatedBy = employeeId; warehouseRequest.UpdatedDate = now;
            }
            await db.WarehouseRequestDetails.Where(x => requestIds.Contains(x.RequestId) && x.IsActive)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false), ct);
        }
        await db.SaveChangesAsync(ct);
        await fulfillment.SyncAsync(order.Id, companyId, employeeId, ct);
        await transaction.CommitAsync(ct);
        return OperationResult.Ok("Xóa mềm phiếu giao hàng thành công.");
    }
}
