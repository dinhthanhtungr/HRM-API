using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Manufacturing.Services.Notifications;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Entities.ManufacturingSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInform;

internal sealed class UpdateProductionOrderInformCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock,
    IEventLogWriter timeline,
    ManufacturingFormulaInputResolver inputResolver,
    ProductionOrderReservationService reservations,
    ManufacturingWorkflowNotifier notifier,
    PromisedDeliveryNotifier promisedDelivery,
    HRM.Application.Features.PLM.SaleOrders.DeliverySchedules.DeliveryScheduleService? deliverySchedules = null
) : IRequestHandler<UpdateProductionOrderInformCommand, OperationResult>
{
    public async Task<OperationResult> Handle(UpdateProductionOrderInformCommand command, CancellationToken ct)
    {
         var error = ManufacturingAccess.ValidateEditor(user, permissions);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        var request = command.Request;
        var notePatch = command.EditorPatch;
        var changesFormulaNote = notePatch?.FormulaNote is not null || notePatch?.ClearFields.Contains("formulaNote") == true;
        if (changesFormulaNote && (!permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingFormulas)
            || !permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials)
            || notePatch!.Action != "Save" || !request.SelectedFormulaId.HasValue || notePatch.FormulaNote?.Length > 20000
            || notePatch.FormulaNote is not null && notePatch.ClearFields.Contains("formulaNote")))
            return OperationResult.Fail("Không có quyền hoặc ghi chú VA không hợp lệ.");
        if (command.Id == Guid.Empty || !command.IsEditorSave && (request.TotalQuantity <= 0 || request.NumOfBatches <= 0
            || request.StepOfProduct is null || !Enum.IsDefined(request.StepOfProduct.Value))
            || request.Items is not null && !request.SelectedFormulaId.HasValue)
        {
            return OperationResult.Fail("Lệnh, lượng, số mẻ, công đoạn hoặc lựa chọn công thức không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockProductionOrderAsync(command.Id, company, ct);
            var order = await db.MfgProductionOrders.FirstOrDefaultAsync(x => x.MfgProductionOrderId == command.Id && x.CompanyId == company && x.IsActive, ct);
            if (order is null)
            {
                return OperationResult.Fail("Không tìm thấy lệnh sản xuất trong công ty.");
            }

            error = ManufacturingAccess.ValidateInformState(user, order.Status);
            if (error is not null) return OperationResult.Fail(error);
            var canManage = ManufacturingAccess.CanManageInform(user, permissions);
            var formulaEditLock = await ManufacturingAccess.FormulaEditLockAsync(db, order, ct);
            if (formulaEditLock is not null && (request.TotalQuantity != (order.TotalQuantity ?? 0)
                || request.NumOfBatches != (order.NumOfBatches ?? 0)))
                return OperationResult.Fail(formulaEditLock);
            var completesRequestedFormula = command.IsEditorSave
                && order.Status == nameof(ManufacturingProductOrder.FormulaRequested)
                && user.IsInRole(ApplicationRoles.Lab.LabUser)
                && permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingFormulas)
                && permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials);
            if (!canManage && (request.LabNote is not null && request.LabNote != order.LabNote
                || request.Requirement is not null && request.Requirement != order.Requirement
                || request.PlpuNote is not null && request.PlpuNote != order.PlpuNote))
                return OperationResult.Fail("LabUser chỉ được sửa QC, ghi chú công thức và NVL trong Inform.");
            if (canManage && order.Status == nameof(ManufacturingProductOrder.WaitingForStandardSample))
                return OperationResult.Fail("Lệnh đang chờ Sale xác nhận mẫu chuẩn; chưa được thao tác kế hoạch.");
            if (!canManage && !ManufacturingEditorPolicy.PlanUnchanged(order, request.TotalQuantity, request.NumOfBatches,
                request.StepOfProduct, request.ExpectedDate, request.ManufacturingDate, request.BagType))
                return OperationResult.Fail("Bạn không có quyền sửa thông tin lệnh sản xuất.");
            error = ManufacturingAccess.ValidatePlpuNoteChange(permissions, order.PlpuNote, request.PlpuNote);
            if (error is not null) return OperationResult.Fail(error);
            error = ManufacturingAccess.ValidateRequirementChange(permissions, order.Requirement, request.Requirement);
            if (error is not null) return OperationResult.Fail(error);
            if (!await ManufacturingEditorRevision.MatchesAsync(db, order, command.ExpectedRevision, ct))
                return OperationResult.Fail(ManufacturingEditorRevision.Conflict);
            if ((command.IsEditorSave || !canManage) && !await db.MfgOrderPOs.AsNoTracking().AnyAsync(x => x.MfgProductionOrderId == command.Id
                && x.IsActive && x.Detail.IsActive && x.Detail.MerchandiseOrder.IsActive
                && x.Detail.MerchandiseOrder.CompanyId == company, ct))
                return OperationResult.Fail("Liên kết đơn hàng không còn active.");
            if (request.Items is not null && !permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials))
                return OperationResult.Fail("Bạn không có quyền sửa thành phần công thức.");

            List<CreateProductionOrderFormulaItemRequest>? items = null;
            string? generatedPlpuNote = null;
            var itemsChanged = false;
            ManufacturingFormula? completedFormula = null;
            if (request.SelectedFormulaId.HasValue || completesRequestedFormula)
            {
                if (request.SelectedFormulaId == Guid.Empty || request.Items is null && !changesFormulaNote && !completesRequestedFormula)
                {
                    return OperationResult.Fail("Thiếu danh sách vật tư của công thức đang chọn.");
                }

                var selectedVersion = await db.ProductionSelectVersions.Where(x => x.MfgProductionOrderId == command.Id && x.CompanyId == company && x.ValidTo == null)
                    .OrderByDescending(x => x.ValidFrom)
                    .ThenByDescending(x => x.ProductionSelectVersionId)
                    .FirstOrDefaultAsync(ct);
                var selected = selectedVersion?.ManufacturingFormulaId;
                if (!selected.HasValue || request.SelectedFormulaId.HasValue && selected != request.SelectedFormulaId)
                {
                    return OperationResult.Fail("Công thức gửi lên không phải công thức đang chọn của lệnh.");
                }

                await db.LockManufacturingFormulaAsync(selected.Value, company, ct);
                var formula = await db.ManufacturingFormulas.Where(x => x.ManufacturingFormulaId == selected && x.CompanyId == company && x.IsActive)
                    .Include(x => x.ManufacturingFormulaMaterials)
                    .FirstOrDefaultAsync(ct);
                if (formula is null)
                {
                    return OperationResult.Fail("Không tìm thấy công thức đang chọn.");
                }
                if (request.Items is not null && formulaEditLock is { } formulaLock)
                {
                    var storedItems = formula.ManufacturingFormulaMaterials.Where(x => x.IsActive).ToList();
                    if (storedItems.Count != request.Items.Count || request.Items.Select(x => x.SourceItemId).Distinct().Count() != storedItems.Count
                        || request.Items.Any(x => !storedItems.Any(s => s.ManufacturingFormulaMaterialId == x.SourceItemId
                            && (s.MaterialId ?? s.ProductId) == x.ItemId
                            && s.itemType == x.ItemType && s.LotNo == x.LotNo
                            && s.Quantity == x.Quantity)))
                        return OperationResult.Fail(formulaLock);
                }
                if (!await ManufacturingEditorRevision.MatchesAsync(db, order, command.ExpectedRevision, ct))
                    return OperationResult.Fail(ManufacturingEditorRevision.Conflict);

                if (request.Items is not null)
                {
                    var resolved = await inputResolver.ResolveAsync(
                        order,
                        new ProductionOrderFormulaOptionDto(
                            formula.ManufacturingFormulaId,
                            formula.ExternalId,
                            ManufacturingFormulaOrigin.Production,
                            command.Id,
                            formula.CreatedDate,
                            true
                        ),
                        request.Items,
                        ct
                    );
                    if (!resolved.Success || resolved.Data is null)
                    {
                        return OperationResult.Fail(resolved.Message ?? "Công thức không hợp lệ.");
                    }

                    items = resolved.Data;
                    generatedPlpuNote = await inputResolver.BuildPlpuNoteAsync(
                        order,
                        new ProductionOrderFormulaOptionDto(
                            formula.ManufacturingFormulaId,
                            formula.ExternalId,
                            ManufacturingFormulaOrigin.Production,
                            command.Id,
                            formula.CreatedDate,
                            true
                        ),
                        request.Items,
                        items,
                        request.PlpuNote ?? order.PlpuNote,
                        ct
                    );
                    itemsChanged = ManufacturingFormulaItemWriter.Replace(formula, request.Items, items,
                        trackNewItem: row => db.ManufacturingFormulaMaterials.Add(row));
                }
                if (changesFormulaNote)
                    formula.Note = notePatch!.ClearFields.Contains("formulaNote") ? null : notePatch.FormulaNote;
                if (completesRequestedFormula)
                {
                    var activeItems = formula.ManufacturingFormulaMaterials.Where(x => x.IsActive).ToList();
                    if (activeItems.Count == 0 || activeItems.Any(x => x.Quantity < 0) || !activeItems.Any(x => x.Quantity > 0))
                        return OperationResult.Fail("Công thức cần có nguyên vật liệu với định mức hợp lệ trước khi hoàn tất.");
                    formula.Status = nameof(ManufacturingProductOrderFormula.Checking);
                    selectedVersion!.ValidFrom ??= clock.Now;
                    completedFormula = formula;
                }
                formula.UpdatedBy = employee;
                formula.UpdatedDate = clock.Now;
            }

            var now = clock.Now;
            var oldExpectedDate = order.ExpectedDate;
            var oldStatus = order.Status;
            var oldQuantity = order.TotalQuantity;
            order.UpdatedBy = employee;
            order.UpdatedDate = now;
            order.ManufacturingDate = request.ManufacturingDate;
            order.ExpectedDate = request.ExpectedDate;
            order.TotalQuantity = command.EditorPatch is { } patch ? patch.TotalQuantity ?? order.TotalQuantity : request.TotalQuantity;
            order.NumOfBatches = command.EditorPatch is { } batchPatch ? batchPatch.NumOfBatches ?? order.NumOfBatches : request.NumOfBatches;
            order.StepOfProduct = request.StepOfProduct;
            PatchHelper.SetIfNotNull(request.LabNote, () => order.LabNote, v => order.LabNote = v);
            var plpu = generatedPlpuNote ?? (request.PlpuNote is null ? null : ManufacturingNoteRules.PreserveStoredPlpuAuto(request.PlpuNote, order.PlpuNote));
            PatchHelper.SetIfNotNull(plpu, () => order.PlpuNote, v => order.PlpuNote = v);
            PatchHelper.SetIfNotNull(request.Requirement, () => order.Requirement, v => order.Requirement = v);
            PatchHelper.SetIfNotNull(request.QcCheck, () => order.QcCheck, v => order.QcCheck = v);
            PatchHelper.SetIfNotNull(request.BagType, () => order.BagType, v => order.BagType = v!);
            if (completedFormula is not null)
            {
                // Lab hoàn tất công thức; PLPU lưu kế hoạch ở bước sau mới chuyển sang Scheduling.
                order.Status = nameof(ManufacturingProductOrder.FormulaSuccess);
                await timeline.AddAsync(new EventLogCreateRequest
                {
                    CompanyId = company,
                    EmployeeId = employee,
                    SourceId = command.Id,
                    SourceCode = order.ExternalId,
                    SourceType = "MfgProductionOrder",
                    EventType = EventType.ManufacturingProductOrder,
                    Status = order.Status,
                    CreatedDate = now,
                    Note = $"Công thức thành công vào {now} bởi {user.UserName}"
                }, ct);
            }
            else if (canManage && order.Status == ManufacturingProductOrder.FormulaSuccess.ToString())
            {
                order.Status = ManufacturingProductOrder.Scheduling.ToString();
            }

            var enteredScheduling = oldStatus != ManufacturingProductOrder.Scheduling.ToString()
                && order.Status == ManufacturingProductOrder.Scheduling.ToString();
            if (order.Status == nameof(ManufacturingProductOrder.Scheduling))
            {
                var selections = await db.ProductionSelectVersions.Where(x => x.CompanyId == company
                    && x.MfgProductionOrderId == command.Id && x.ValidTo == null
                    && x.ManufacturingFormula != null && x.ManufacturingFormula.IsActive
                    && x.ManufacturingFormula.CompanyId == company).AsTracking().ToListAsync(ct);
                var activation = ManufacturingSchedulingSelectionRules.Activate(selections, now);
                if (!activation.Success) return activation;
            }
            var schedule = await db.SchedualMfgs.FirstOrDefaultAsync(x => x.MfgProductionOrderId == command.Id, ct);
            if (canManage && completedFormula is null && schedule is null
                && !string.Equals(
                order.Status,
                ManufacturingProductOrder.FormulaRequested.ToString(),
                StringComparison.OrdinalIgnoreCase
            )
                && !string.Equals(order.Status, ManufacturingProductOrder.Stocked.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                await db.SchedualMfgs.AddAsync(ProductionOrderFormulaFactory.CreateSchedule(order, now), ct);
            }

            if (schedule is not null)
            {
                schedule.DeliveryPlanDate = order.ExpectedDate;
                schedule.Note = order.LabNote ?? order.PlpuNote;
                schedule.requirement = order.Requirement;
                schedule.StepOfProduct = order.StepOfProduct;
            }

            if (enteredScheduling)
            {
                await timeline.AddAsync(new EventLogCreateRequest
                {
                    CompanyId = company,
                    EmployeeId = employee,
                    SourceId = command.Id,
                    SourceCode = order.ExternalId,
                    SourceType = "MfgProductionOrder",
                    EventType = EventType.ManufacturingProductOrder,
                    Status = order.Status,
                    CreatedDate = now,
                    Note = $"Chuyển sang chờ xếp lịch vào {now:dd/MM/yyyy HH:mm:ss} bởi {user.UserName}"
                }, ct);
            } // Legacy full form always supplies TotalQuantity. Header-only requests do not resync stock.

            if (items is { Count: > 0 } && (request.TotalQuantity > 0 || itemsChanged || enteredScheduling))
            {
                var result = await reservations.SyncAsync(order, items, now, ct);
                if (!result.Success)
                {
                    return OperationResult.Fail(result.Message ?? "Không thể đồng bộ giữ chỗ vật tư.");
                }
            }

            if (command.IsEditorSave && items is null && order.Status != ManufacturingProductOrder.Stocked.ToString()
                && (oldQuantity != order.TotalQuantity || enteredScheduling))
            {
                var hasSelection = await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == company
                    && x.MfgProductionOrderId == command.Id && x.ValidTo == null && x.ManufacturingFormulaId != null, ct);
                if (hasSelection)
                {
                    var result = await ManufacturingInformationActionRules.ReserveAsync(db, reservations, order, now, ct);
                    if (!result.Success) return result;
                }
            }

            if (completedFormula is not null)
            {
                var link = await db.MfgOrderPOs.AsNoTracking().Where(x => x.MfgProductionOrderId == command.Id
                    && x.IsActive && x.Detail.IsActive && x.Detail.MerchandiseOrder.IsActive
                    && x.Detail.MerchandiseOrder.CompanyId == company)
                    .Select(x => new { x.Detail.MerchandiseOrderId, x.Detail.UnitPriceAgreed })
                    .FirstOrDefaultAsync(ct);
                if (link is not null)
                {
                    var sale = await db.MerchandiseOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MerchandiseOrderId == link.MerchandiseOrderId
                        && x.CompanyId == company && x.IsActive, ct);
                    if (sale is not null)
                        await notifier.PriceWarningAsync(order, completedFormula, sale, link.UnitPriceAgreed, ct);
                }
            }

            if (notePatch?.DeliveryPromises is { } promises)
            {
                if (!command.IsEditorSave || notePatch.Action != "Save" || deliverySchedules is null)
                    return OperationResult.Fail("Không thể lưu ngày hứa cùng thông tin MFG.");
                var promiseResult = await deliverySchedules.SaveManufacturingPromisesAsync(command.Id, promises, transaction.TransactionId, ct);
                if (!promiseResult.Success) return promiseResult;
            }
            await db.SaveChangesAsync(ct);
            if (order.Status == nameof(ManufacturingProductOrder.Scheduling)
                && !await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == company
                    && x.MfgProductionOrderId == command.Id && x.ValidFrom != null && x.ValidTo == null
                    && x.ManufacturingFormula != null && x.ManufacturingFormula.IsActive
                    && x.ManufacturingFormula.CompanyId == company, ct))
                return OperationResult.Fail("Không thể lưu Chờ lịch: VA chưa được kích hoạt. Thao tác đã được hủy.");
            await promisedDelivery.PublishAsync(order.MfgProductionOrderId, order.ExternalId, oldExpectedDate, order.ExpectedDate, ct);
            await transaction.CommitAsync(ct);
            return OperationResult.Ok("Cập nhật thông tin và công thức VA thành công.");
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return OperationResult.Fail("Không thể cập nhật thông tin lệnh sản xuất.");
        }
    }
}
