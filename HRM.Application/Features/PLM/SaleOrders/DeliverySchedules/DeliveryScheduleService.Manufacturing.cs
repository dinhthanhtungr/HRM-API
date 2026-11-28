using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

public sealed partial class DeliveryScheduleService
{
    // Không mở/commit transaction riêng: toàn bộ thay đổi thuộc writer MFG.
    public async Task<OperationResult> SaveManufacturingPromisesAsync(Guid mfgId,
        IReadOnlyList<ManufacturingDeliveryPromisePatch> patches, Guid transactionId, CancellationToken ct)
    {
        if (db.DeliveryScheduleTransactionId != transactionId || !ValidUser() || !UiEnabled
            || !permissions.HasPermission(ApplicationPermissions.PLM.EditPromisedDeliverySchedule))
            return OperationResult.Fail("Không thể lưu ngày hứa trong transaction MFG hoặc không đủ quyền.");
        if (patches.Count is < 1 or > 100 || patches.Any(x => x is null)
            || patches.Select(x => x.DetailId).Distinct().Count() != patches.Count)
            return OperationResult.Fail("Danh sách ngày hứa không hợp lệ.");
        foreach (var patch in patches.OrderBy(x => x.DetailId))
        {
            if (patch.OrderId == Guid.Empty || patch.DetailId == Guid.Empty || string.IsNullOrWhiteSpace(patch.Revision) || patch.Revision.Length != 64
                || patch.Dates is null || patch.Dates.Count is < 1 or > 100 || patch.Dates.Any(x => x is null || x.ScheduleId == Guid.Empty)
                || patch.Dates.Select(x => x.ScheduleId).Distinct().Count() != patch.Dates.Count)
                return OperationResult.Fail("Ngày hứa hoặc revision không hợp lệ.");
            var linked = await db.MfgOrderPOs.AsNoTracking().AnyAsync(x => x.IsActive && x.MfgProductionOrderId == mfgId
                && x.ProductionOrder.IsActive && x.ProductionOrder.CompanyId == user.CompanyId
                && x.Detail.IsActive && x.Detail.MerchandiseOrderDetailId == patch.DetailId
                && x.Detail.MerchandiseOrderId == patch.OrderId && x.Detail.MerchandiseOrder.IsActive
                && x.Detail.MerchandiseOrder.CompanyId == user.CompanyId, ct);
            if (!linked) return OperationResult.Fail("Đợt giao không thuộc dòng đơn liên kết với MFG trong công ty.");
            await db.LockDeliveryScheduleAsync(user.CompanyId!.Value, patch.DetailId, ct);
            var current = await GetAsync(patch.OrderId, patch.DetailId, ct);
            if (current is null || !current.CanEditPromise) return OperationResult.Fail("Không có quyền sửa ngày hứa hoặc đơn đã đóng.");
            if (patch.Revision != current.Revision) return OperationResult.Fail("Lịch hoặc lượng giao đã thay đổi. Vui lòng tải lại.");
            if (current.Items.Count < 2 || patch.Dates.Any(x => !current.Items.Any(row => row.Id == x.ScheduleId)))
                return OperationResult.Fail("Đợt giao không còn hợp lệ; không tự chuyển đổi luồng một ngày.");
            var dates = patch.Dates.ToDictionary(x => x.ScheduleId, x => x.PromisedDate);
            var items = current.Items.Select(row => new DeliveryScheduleInput(row.Id, row.Quantity, row.RequestedDate,
                dates.TryGetValue(row.Id, out var date) ? date : row.PromisedDate, row.RequestNote, row.PromiseNote)).ToArray();
            var result = await SaveInTransactionAsync(patch.OrderId, patch.DetailId, new(patch.Revision, items, []), ct);
            if (!result.Success) return result;
        }
        return OperationResult.Ok();
    }
}
