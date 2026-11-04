using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Commons.Deliveries;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Services;

/// <summary>Đồng bộ lượng và trạng thái DHG từ phiếu đã lưu, trong transaction của caller.</summary>
internal sealed class DeliveryOrderFulfillmentService(IDispatchWriteDbContext db)
{
    public async Task SyncAsync(Guid deliveryOrderId, Guid companyId, Guid employeeId, CancellationToken ct)
    {
        // Bao gồm dòng lịch sử PUT để cập nhật cả PO vừa bị bỏ khỏi phiếu.
        var poIds = await db.DeliveryOrderDetails.AsNoTracking()
            .Where(x => x.DeliveryOrderId == deliveryOrderId && !x.IsAttach &&
                x.DeliveryOrder.CompanyId == companyId && x.MerchandiseOrderDetailId.HasValue)
            .Select(x => x.MerchandiseOrderDetail!.MerchandiseOrderId).Distinct().ToListAsync(ct);

        // Khóa các header theo cùng thứ tự trước khi đọc tổng: hai phiếu lưu đồng thời
        // cho cùng DHG không ghi đè lượng của nhau bằng snapshot cũ.
        foreach (var poId in poIds.OrderBy(x => x))
            await db.MerchandiseOrders.Where(x => x.MerchandiseOrderId == poId && x.CompanyId == companyId && x.IsActive)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, x => x.Status), ct);

        var lines = db.MerchandiseOrderDetails.Where(x => poIds.Contains(x.MerchandiseOrderId) &&
            x.IsActive && x.MerchandiseOrder.IsActive && x.MerchandiseOrder.CompanyId == companyId);
        var effective = db.DeliveryOrderDetails.EffectiveDeliveryLines(companyId);
        await lines.ExecuteUpdateAsync(set => set.SetProperty(x => x.RealQuantity, x =>
            effective.Where(d => d.MerchandiseOrderDetailId == x.MerchandiseOrderDetailId)
                .Sum(d => (decimal?)d.Quantity) ?? 0m), ct);
        await lines.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, x =>
            (x.RealQuantity ?? 0m) <= 0m ? nameof(MerchadiseStatus.Processing)
                : x.ExpectedQuantity > 0m && (x.RealQuantity ?? 0m) >= x.ExpectedQuantity
                    ? nameof(MerchadiseStatus.Delivered) : nameof(MerchadiseStatus.Delivering)), ct);
        await db.MerchandiseOrders.Where(x => poIds.Contains(x.MerchandiseOrderId) && x.CompanyId == companyId && x.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, x =>
                x.MerchandiseOrderDetails.Any(d => d.IsActive) &&
                x.MerchandiseOrderDetails.Where(d => d.IsActive).All(d => d.ExpectedQuantity > 0m && (d.RealQuantity ?? 0m) >= d.ExpectedQuantity)
                    ? nameof(MerchadiseStatus.Delivered)
                    : x.MerchandiseOrderDetails.Any(d => d.IsActive && (d.RealQuantity ?? 0m) > 0m)
                        ? nameof(MerchadiseStatus.Delivering) : nameof(MerchadiseStatus.Processing))
                .SetProperty(x => x.UpdatedBy, employeeId).SetProperty(x => x.UpdatedDate, DateTime.Now), ct);
    }
}
