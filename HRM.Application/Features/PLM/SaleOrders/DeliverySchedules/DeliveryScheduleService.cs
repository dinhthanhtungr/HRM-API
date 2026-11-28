using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM.DeliverySchedules;
using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

public sealed partial class DeliveryScheduleService(IDeliveryScheduleDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility,
    IDateTimeProvider clock, IOptions<DeliveryScheduleOptions> options, DeliveryScheduleFulfillmentService fulfillment,
    DeliverySchedulePromiseNotifier? promiseNotifications = null)
{
    public bool StorageReady => options.Value.StorageReady;
    public bool UiEnabled => options.Value.CanShowMultiScheduleUi;
    public DeliveryScheduleFeatureDto Features() => new(ValidUser() && UiEnabled,
        ValidUser() && UiEnabled && permissions.HasPermission(ApplicationPermissions.PLM.EditRequestedDeliverySchedule),
        ValidUser() && UiEnabled && permissions.HasPermission(ApplicationPermissions.PLM.EditPromisedDeliverySchedule));

    public string? PrepareNewDetail(MerchandiseOrderDetail detail, IReadOnlyList<DeliveryScheduleInput>? items, Guid employee, DateTime now)
    {
        if (items is null) return null;
        if (!UiEnabled || !permissions.HasPermission(ApplicationPermissions.PLM.EditRequestedDeliverySchedule))
            return "Luồng chia đợt chưa bật hoặc không có quyền chia lịch yêu cầu.";
        var error = DeliveryScheduleSaveRules.Validate(detail.ExpectedQuantity, [], new("", items, []), true,
            permissions.HasPermission(ApplicationPermissions.PLM.EditPromisedDeliverySchedule));
        if (error is not null) return error;
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok")));
        if (items.Any(x => x.RequestedDate < localToday))
            return "Ngày yêu cầu của đợt trong đơn mới không được trước ngày tạo đơn.";
        foreach (var item in items)
            detail.DeliverySchedules.Add(new() { MerchandiseOrderDeliveryScheduleId = Guid.CreateVersion7(),
                MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId, Quantity = item.Quantity,
                RequestedDate = item.RequestedDate.ToDateTime(TimeOnly.MinValue), PromisedDate = item.PromisedDate?.ToDateTime(TimeOnly.MinValue),
                RequestNote = item.RequestNote?.Trim(), PromiseNote = item.PromiseNote?.Trim(), CreatedBy = employee, UpdatedBy = employee,
                CreatedDate = now, UpdatedDate = now });
        detail.DeliveryRequestDate = items.Min(x => x.RequestedDate).ToDateTime(TimeOnly.MinValue);
        return null;
    }
    public async Task<DeliveryScheduleWorkspaceDto?> GetAsync(Guid orderId, Guid detailId, CancellationToken ct, Guid? excludingDeliveryOrderId = null)
    {
        if (!ValidUser()) return null;
        if (!UiEnabled) return new(false, false, false, "", 0, null, null, []);
        var detail = await FindAsync(orderId, detailId, false, ct);
        if (detail is null) return null;
        var rows = await db.MerchandiseOrderDeliverySchedules.AsNoTracking()
            .Where(x => x.MerchandiseOrderDetailId == detailId && x.IsActive)
            .OrderBy(x => x.MerchandiseOrderDeliveryScheduleId).ToListAsync(ct);
        var counted = db.DeliveryOrderDetails.AsNoTracking().CountedForAllocation(user.CompanyId!.Value, excludingDeliveryOrderId);
        var allocations = await db.DeliveryOrderScheduleAllocations.AsNoTracking()
            .Where(a => a.IsActive && a.Schedule != null && a.MerchandiseOrderDeliveryScheduleId.HasValue && a.Schedule.MerchandiseOrderDetailId == detailId
                && counted.Any(d => d.Id == a.DeliveryOrderDetailId && d.MerchandiseOrderDetailId == detailId))
            .GroupBy(a => a.MerchandiseOrderDeliveryScheduleId!.Value)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(a => a.Quantity) }).ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        var unreconciled = await counted.AnyAsync(d => d.MerchandiseOrderDetailId == detailId
            && (rows.Count == 0 || !db.DeliveryOrderScheduleAllocations.Any(a => a.IsActive && a.DeliveryOrderDetailId == d.Id)), ct);
        var output = rows.Select(x => new DeliveryScheduleRowDto(x.MerchandiseOrderDeliveryScheduleId, x.Quantity,
            allocations.GetValueOrDefault(x.MerchandiseOrderDeliveryScheduleId), DateOnly.FromDateTime(x.RequestedDate),
            x.PromisedDate.HasValue ? DateOnly.FromDateTime(x.PromisedDate.Value) : null, x.RequestNote, x.PromiseNote)).ToArray();
        var hasEditCapability = permissions.HasPermission(ApplicationPermissions.PLM.EditRequestedDeliverySchedule)
            || permissions.HasPermission(ApplicationPermissions.PLM.EditPromisedDeliverySchedule);
        var writable = !Closed(detail.MerchandiseOrder.Status) && !unreconciled && hasEditCapability
            && (DeliveryOrderAccessRules.CanManage(user) || await FindAsync(orderId, detailId, true, ct) is not null);
        var legacyPromise = detail.ExpectedDeliveryDate;
        if (rows.Count == 0 && !legacyPromise.HasValue)
            legacyPromise = await db.MfgOrderPOs.AsNoTracking().Where(x => x.IsActive && x.MerchandiseOrderDetailId == detailId
                && x.ProductionOrder.CompanyId == user.CompanyId && x.ProductionOrder.IsActive
                && x.ProductionOrder.Status.Trim().ToLower() != "canceled" && x.ProductionOrder.Status.Trim().ToLower() != "cancelled")
                .Select(x => x.ProductionOrder.ExpectedDate).MaxAsync(ct);
        return new(true, writable && permissions.HasPermission(ApplicationPermissions.PLM.EditRequestedDeliverySchedule),
            writable && permissions.HasPermission(ApplicationPermissions.PLM.EditPromisedDeliverySchedule),
            Revision(detail, output, legacyPromise), detail.ExpectedQuantity, DateOnly.FromDateTime(detail.DeliveryRequestDate),
            legacyPromise.HasValue ? DateOnly.FromDateTime(legacyPromise.Value) : null,
            output, unreconciled);
    }

    public async Task<OperationResult> SaveAsync(Guid orderId, Guid detailId, SaveDeliverySchedulesRequest request, CancellationToken ct)
    {
        if (!ValidUser() || !UiEnabled) return OperationResult.Fail("Luồng chia đợt chưa bật hoặc tài khoản không hợp lệ.");
        if (request.Items is null || request.RemoveIds is null || string.IsNullOrWhiteSpace(request.Revision)
            || request.Items.Count is < 1 or > 100 || request.Items.Any(x => x is null))
            return OperationResult.Fail("Thiếu lịch giao hoặc revision.");
        await using var tx = await db.BeginDeliveryScheduleTransactionAsync(ct);
        var result = await SaveInTransactionAsync(orderId, detailId, request, ct);
        if (result.Success) await tx.CommitAsync(ct);
        return result;
    }

    private async Task<OperationResult> SaveInTransactionAsync(Guid orderId, Guid detailId, SaveDeliverySchedulesRequest request, CancellationToken ct,
        bool notifySingleSchedule = false)
    {
        await db.LockDeliveryScheduleAsync(user.CompanyId!.Value, detailId, ct);
        var detail = await FindAsync(orderId, detailId, true, ct);
        if (detail is null) return OperationResult.Fail("Không có quyền sửa dòng đơn hàng.");
        var current = await GetAsync(orderId, detailId, ct);
        if (current is null || (!current.CanEditRequest && !current.CanEditPromise))
            return OperationResult.Fail("Không có quyền sửa lịch, đơn đã đóng hoặc phiếu cũ chưa được đối chiếu đợt.");
        if (request.Revision != current.Revision) return OperationResult.Fail("Lịch hoặc lượng giao đã thay đổi. Vui lòng tải lại.");
        var bootstrapPromise = current.Items.Count == 0 && request.Items.Count == 1 && request.RemoveIds.Count == 0
            && request.Items[0].Id is null && request.Items[0].Quantity == detail.ExpectedQuantity
            && request.Items[0].RequestedDate == DateOnly.FromDateTime(detail.DeliveryRequestDate)
            && string.IsNullOrEmpty(request.Items[0].RequestNote) && current.CanEditPromise;
        var preserveLegacyPromise = current.Items.Count == 0 && request.Items.All(x => x.PromisedDate == current.LegacyPromisedDate && string.IsNullOrEmpty(x.PromiseNote));
        var error = DeliveryScheduleSaveRules.Validate(detail.ExpectedQuantity, current.Items, request,
            current.CanEditRequest || bootstrapPromise, current.CanEditPromise || preserveLegacyPromise);
        if (error is not null) return OperationResult.Fail(error);
        if (request.RemoveIds.Count == 0 && request.Items.Count == current.Items.Count && request.Items.All(input =>
            current.Items.Any(row => input.Id == row.Id && input.Quantity == row.Quantity && input.RequestedDate == row.RequestedDate
                && input.PromisedDate == row.PromisedDate && input.RequestNote?.Trim() == row.RequestNote?.Trim()
                && input.PromiseNote?.Trim() == row.PromiseNote?.Trim()))) return OperationResult.Ok();
        var rows = await db.MerchandiseOrderDeliverySchedules.Where(x => x.MerchandiseOrderDetailId == detailId && x.IsActive).AsTracking().ToListAsync(ct);
        var promiseChanges = new List<DeliverySchedulePromiseChange>();
        foreach (var id in request.RemoveIds) rows.Single(x => x.MerchandiseOrderDeliveryScheduleId == id).IsActive = false;
        foreach (var input in request.Items)
        {
            var row = input.Id.HasValue ? rows.Single(x => x.MerchandiseOrderDeliveryScheduleId == input.Id.Value)
                : new MerchandiseOrderDeliverySchedule { MerchandiseOrderDeliveryScheduleId = Guid.CreateVersion7(),
                    MerchandiseOrderDetailId = detailId, CreatedBy = user.EmployeeId!.Value, CreatedDate = clock.Now };
            if (!input.Id.HasValue) db.MerchandiseOrderDeliverySchedules.Add(row);
            row.Quantity = input.Quantity;
            row.RequestedDate = input.RequestedDate.ToDateTime(TimeOnly.MinValue);
            row.PromisedDate = input.PromisedDate?.ToDateTime(TimeOnly.MinValue);
            row.RequestNote = input.RequestNote?.Trim(); row.PromiseNote = input.PromiseNote?.Trim();
            row.UpdatedBy = user.EmployeeId!.Value; row.UpdatedDate = clock.Now;
            var previousPromise = current.Items.FirstOrDefault(x => x.Id == input.Id)?.PromisedDate;
            if ((notifySingleSchedule || current.Items.Count > 1 || request.Items.Count > 1) && previousPromise != input.PromisedDate)
                promiseChanges.Add(new(row.MerchandiseOrderDeliveryScheduleId, input.Quantity, input.RequestedDate, previousPromise, input.PromisedDate));
        }
        // Legacy fields remain a summary, never change prices/payment/quantity here.
        detail.DeliveryRequestDate = request.Items.Min(x => x.RequestedDate).ToDateTime(TimeOnly.MinValue);
        detail.ExpectedDeliveryDate = request.Items.Where(x => x.PromisedDate.HasValue
            && x.Quantity > (current.Items.FirstOrDefault(row => row.Id == x.Id)?.AllocatedQuantity ?? 0)).Select(x => x.PromisedDate!.Value)
            .Order().Select(x => (DateTime?)x.ToDateTime(TimeOnly.MinValue)).FirstOrDefault();
        db.AuditLogs.Add(new AuditLog { CompanyId = user.CompanyId!.Value, SchemaName = "Orders", TableName = "MerchandiseOrderDeliverySchedules",
            RecordId = detailId, ActionType = AuditActionType.Update, ChangedBy = user.EmployeeId!.Value, ChangedAt = clock.Now,
            OldValues = JsonSerializer.SerializeToDocument(current.Items), NewValues = JsonSerializer.SerializeToDocument(request), Reason = "Update delivery schedules" });
        await db.SaveChangesAsync(ct);
        // Existing fulfillment/cancel flows own their row-lock ordering. Do not
        // take an order row lock before their detail updates; recheck committed
        // lifecycle state instead of introducing an inverted lock hierarchy.
        var statusAtCommit = await db.MerchandiseOrders.AsNoTracking().Where(x => x.CompanyId == user.CompanyId && x.IsActive && x.MerchandiseOrderId == orderId)
            .Select(x => x.Status).FirstOrDefaultAsync(ct);
        if (statusAtCommit is null || Closed(statusAtCommit)) return OperationResult.Fail("Đơn đã đóng trong lúc lưu lịch. Thay đổi lịch đã được hoàn tác.");
        if (promiseNotifications is not null && promiseChanges.Count > 0)
            await promiseNotifications.PublishAsync(detail, promiseChanges, ct);
        return OperationResult.Ok();
    }

    public async Task<OperationResult<DeliverySchedulePreviewDto>> PreviewAsync(Guid orderId, Guid detailId, DeliverySchedulePreviewRequest request, CancellationToken ct)
    {
        if (!ValidUser()) return OperationResult<DeliverySchedulePreviewDto>.Fail("Tài khoản không hợp lệ.");
        if (!UiEnabled) return OperationResult<DeliverySchedulePreviewDto>.Ok(new DeliverySchedulePreviewDto(false, [], [], 0));
        if (request.ExcludingDeliveryOrderId is { } deliveryId && !await db.DeliveryOrderDetails.AsNoTracking().AnyAsync(x =>
            x.DeliveryOrderId == deliveryId && x.MerchandiseOrderDetailId == detailId && x.DeliveryOrder.CompanyId == user.CompanyId, ct))
            return OperationResult<DeliverySchedulePreviewDto>.Fail("Phiếu giao không thuộc dòng đơn/công ty.");
        var workspace = await GetAsync(orderId, detailId, ct, request.ExcludingDeliveryOrderId);
        if (workspace is null) return OperationResult<DeliverySchedulePreviewDto>.Fail("Không có quyền xem dòng đơn.");
        try
        {
            var capacities = workspace.Items.Select(x => new DeliveryScheduleCapacity(x.Id, x.Quantity, x.AllocatedQuantity, x.RequestedDate, x.PromisedDate)).ToArray();
            var existing = request.ExcludingDeliveryOrderId.HasValue
                ? await fulfillment.ExistingPlanAsync(request.ExcludingDeliveryOrderId.Value, detailId, request.Quantity, user.CompanyId!.Value, ct) : null;
            var plan = existing is null ? DeliveryScheduleAllocationRules.Suggest(request.Quantity, capacities)
                : DeliveryScheduleAllocationRules.ValidateManual(request.Quantity, capacities, existing);
            return OperationResult<DeliverySchedulePreviewDto>.Ok(new DeliverySchedulePreviewDto(true, workspace.Items, plan.Items, plan.UnscheduledQuantity));
        }
        catch (ArgumentException) { return OperationResult<DeliverySchedulePreviewDto>.Fail("Số lượng hoặc lịch giao không hợp lệ."); }
    }

    private bool ValidUser() => user.IsAuthenticated && user.CompanyId is { } c && c != Guid.Empty && user.EmployeeId is { } e && e != Guid.Empty;
    private static bool Closed(string status) => new[] { "Canceled", "Cancelled", "Delivered", "Completed" }.Contains(status?.Trim(), StringComparer.OrdinalIgnoreCase);
    private async Task<MerchandiseOrderDetail?> FindAsync(Guid orderId, Guid detailId, bool write, CancellationToken ct)
    {
        var company = user.CompanyId!.Value;
        var orders = db.MerchandiseOrders.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive);
        if (!DeliveryOrderAccessRules.CanManage(user))
        {
            var scope = write ? await visibility.BuildScopeAsync(ct) : await visibility.BuildReadScopeAsync(ct);
            if (scope.CompanyId != company || scope.EmployeeId != user.EmployeeId) return null;
            if (write) scope = scope with { HasFullCustomerView = false };
            orders = visibility.ApplyMerchandiseOrderVisibility(orders, db.Customers, scope);
        }
        return await db.MerchandiseOrderDetails.Include(x => x.MerchandiseOrder).AsTracking()
            .FirstOrDefaultAsync(x => x.IsActive && x.MerchandiseOrderId == orderId && x.MerchandiseOrderDetailId == detailId
                && orders.Any(o => o.MerchandiseOrderId == x.MerchandiseOrderId), ct);
    }
    private static string Revision(MerchandiseOrderDetail d, IReadOnlyList<DeliveryScheduleRowDto> rows, DateTime? legacyPromise)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { d.ExpectedQuantity,
            d.DeliveryRequestDate, d.ExpectedDeliveryDate, LegacyPromise = legacyPromise, Rows = rows.OrderBy(x => x.Id) }))));
}
