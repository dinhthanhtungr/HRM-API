using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;

namespace HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

public sealed partial class DeliveryScheduleService
{
    /// <summary>
    /// Cập nhật các ngày hứa được chỉ định của lịch đã lưu. Không thay ngày yêu cầu, lượng hay cấu trúc đợt.
    /// Ngày hứa và notification/outbox dùng chung transaction; dữ liệu legacy tiếp tục qua writer MFG cũ.
    /// </summary>
    public async Task<OperationResult<DeliveryScheduleWorkspaceDto>> UpdatePromisesAsync(Guid orderId, Guid detailId,
        UpdateDeliveryPromisesRequest request, CancellationToken ct)
    {
        if (!ValidUser() || !UiEnabled || !permissions.HasPermission(ApplicationPermissions.PLM.EditPromisedDeliverySchedule))
            return OperationResult<DeliveryScheduleWorkspaceDto>.Fail("Chưa bật lịch giao hoặc không có quyền sửa ngày hứa.");
        if (request is null || string.IsNullOrWhiteSpace(request.Revision) || request.Revision.Length != 64
            || request.Items is null || request.Items.Count is < 1 or > 100
            || request.Items.Any(x => x is null || x.ScheduleId == Guid.Empty || x.PromisedDate == default(DateOnly))
            || request.Items.Select(x => x.ScheduleId).Distinct().Count() != request.Items.Count)
            return OperationResult<DeliveryScheduleWorkspaceDto>.Fail("Ngày hứa, ID đợt hoặc revision không hợp lệ.");

        await using var tx = await db.BeginDeliveryScheduleTransactionAsync(ct);
        await db.LockDeliveryScheduleAsync(user.CompanyId!.Value, detailId, ct);
        var current = await GetAsync(orderId, detailId, ct);
        if (current is null || !current.CanEditPromise)
            return OperationResult<DeliveryScheduleWorkspaceDto>.Fail("Không có quyền sửa lịch, đơn đã đóng hoặc phiếu cũ chưa được đối chiếu đợt.");
        if (request.Revision != current.Revision)
            return OperationResult<DeliveryScheduleWorkspaceDto>.Fail("Lịch hoặc lượng giao đã thay đổi. Vui lòng tải lại.");
        if (current.Items.Count == 0)
            return OperationResult<DeliveryScheduleWorkspaceDto>.Fail("Dòng đang dùng một ngày theo luồng cũ. Hãy lưu ngày qua thao tác MFG hiện có.");
        if (request.Items.Any(x => !current.Items.Any(row => row.Id == x.ScheduleId)))
            return OperationResult<DeliveryScheduleWorkspaceDto>.Fail("Đợt giao không thuộc dòng đơn hoặc đã bị gỡ. Vui lòng tải lại.");

        var dates = request.Items.ToDictionary(x => x.ScheduleId, x => x.PromisedDate);
        var items = current.Items.Select(row => new DeliveryScheduleInput(row.Id, row.Quantity, row.RequestedDate,
            dates.TryGetValue(row.Id, out var date) ? date : row.PromisedDate, row.RequestNote, row.PromiseNote)).ToArray();
        var saved = await SaveInTransactionAsync(orderId, detailId, new(current.Revision, items, []), ct, notifySingleSchedule: true);
        if (!saved.Success) return OperationResult<DeliveryScheduleWorkspaceDto>.Fail(saved.Message!);
        var output = await GetAsync(orderId, detailId, ct);
        if (output is null || !output.Enabled)
            return OperationResult<DeliveryScheduleWorkspaceDto>.Fail("Không thể đọc lại lịch; thay đổi đã được hoàn tác.");
        await tx.CommitAsync(ct);
        return OperationResult<DeliveryScheduleWorkspaceDto>.Ok(output);
    }
}
