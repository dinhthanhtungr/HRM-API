using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using HRM.Application.Features.PLM.Manufacturing.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.UseProductionOrderStock;

/// <summary>New → Stocked, đồng bộ ngày lịch và đơn hàng; không tạo phiếu xuất hoặc trừ tồn.</summary>
internal sealed class UseProductionOrderStockCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock,
    IEventLogWriter timeline,
    PromisedDeliveryNotifier promisedDelivery,
    HRM.Application.Features.PLM.SaleOrders.DeliverySchedules.DeliveryScheduleService? deliverySchedules = null
) : IRequestHandler<UseProductionOrderStockCommand, OperationResult>
{
    public async Task<OperationResult> Handle(UseProductionOrderStockCommand command, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ManageProductionOrders, true);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        var request = command.Request;
        if (command.Id == Guid.Empty || request.TotalQuantity <= 0 || request.NumOfBatches <= 0
            || request.StepOfProduct is null
            || !Enum.IsDefined(request.StepOfProduct.Value))
        {
            return OperationResult.Fail("Lệnh, khối lượng, số mẻ hoặc công đoạn sản phẩm không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockProductionOrderAsync(command.Id, company, ct);
            var link = await db.MfgOrderPOs.Where(x => x.IsActive && x.MfgProductionOrderId == command.Id && x.ProductionOrder.IsActive
                && x.ProductionOrder.CompanyId == company
                && x.Detail.IsActive
                && x.Detail.MerchandiseOrder.IsActive
                && x.Detail.MerchandiseOrder.CompanyId == company)
                .Include(x => x.ProductionOrder)
                .Include(x => x.Detail)
                .FirstOrDefaultAsync(ct);
            if (link is null)
            {
                return OperationResult.Fail("Không tìm thấy lệnh sản xuất liên kết đơn hàng trong công ty.");
            }

            var order = link.ProductionOrder;
            if (!ManufacturingAccess.IsInformOperator(user))
                return OperationResult.Fail("Chỉ PLPUUser, President và Developer được yêu cầu xuất kho.");
            error = ManufacturingAccess.ValidatePlpuNoteChange(permissions, order.PlpuNote, request.PlpuNote);
            if (error is not null) return OperationResult.Fail(error);
            error = ManufacturingAccess.ValidateRequirementChange(permissions, order.Requirement, request.Requirement);
            if (error is not null) return OperationResult.Fail(error);
            if (!await ManufacturingEditorRevision.MatchesAsync(db, order, command.ExpectedRevision, ct))
                return OperationResult.Fail(ManufacturingEditorRevision.Conflict);
            if (order.Status != ManufacturingProductOrder.New.ToString())
            {
                return OperationResult.Fail("Chỉ có thể dùng hàng tồn cho lệnh mới. Vui lòng tải lại dữ liệu.");
            }

            string? labNote = request.LabNote;
            if (command.SourceFormulaId.HasValue)
            {
                var sources = await ManufacturingFormulaOptionReader.ReadAsync(db, order, ct);
                var source = sources.FirstOrDefault(x => x.FormulaId == command.SourceFormulaId.Value);
                if (source is null) return OperationResult.Fail("Nguồn công thức không còn hợp lệ của lệnh.");
                labNote = ManufacturingNoteRules.MergeLab(labNote ?? order.LabNote, source.Note);
            }
            var now = clock.Now;
            order.UpdatedBy = employee;
            order.UpdatedDate = now;
            // Legacy action resets CreatedDate. Kept explicitly, separate from audit UpdatedDate.
            order.CreatedDate = now;
            order.StepOfProduct = request.StepOfProduct;
            order.TotalQuantity = request.TotalQuantity;
            order.NumOfBatches = request.NumOfBatches;
            var previousExpectedDate = order.ExpectedDate;
            order.ExpectedDate = request.ExpectedDate;
            PatchHelper.SetIfNotNull(request.PlpuNote, () => order.PlpuNote, v => order.PlpuNote = v);
            PatchHelper.SetIfNotNull(labNote, () => order.LabNote, v => order.LabNote = v);
            PatchHelper.SetIfNotNull(request.Requirement, () => order.Requirement, v => order.Requirement = v);
            PatchHelper.SetIfNotNull(request.QcCheck, () => order.QcCheck, v => order.QcCheck = v);
            order.Status = ManufacturingProductOrder.Stocked.ToString();
            await timeline.AddAsync(new EventLogCreateRequest
            {
                CompanyId = company,
                EmployeeId = employee,
                SourceId = order.MfgProductionOrderId,
                SourceCode = order.ExternalId,
                SourceType = "MfgProductionOrder",
                EventType = EventType.ManufacturingProductOrder,
                Status = order.Status,
                CreatedDate = now,
                Note = $"Xuất tồn kho bởi {user.UserName} vào {now}"
            }, ct);
            var schedule = await db.SchedualMfgs.FirstOrDefaultAsync(x => x.MfgProductionOrderId == command.Id, ct);
            if (schedule is not null)
            {
                schedule.DeliveryPlanDate = order.ExpectedDate;
            }

            var sale = await db.MerchandiseOrders.FirstOrDefaultAsync(x => x.MerchandiseOrderId == link.Detail.MerchandiseOrderId && x.CompanyId == company && x.IsActive, ct);
            if (sale is not null)
            {
                if (!Enum.TryParse<MerchadiseStatus>(sale.Status, true, out var status))
                {
                    status = MerchadiseStatus.New;
                }

                if (status is MerchadiseStatus.New or MerchadiseStatus.Approved)
                {
                    sale.Status = MerchadiseStatus.Processing.ToString();
                    sale.UpdatedBy = employee;
                    sale.UpdatedDate = now;
                    await timeline.AddAsync(new EventLogCreateRequest
                    {
                        CompanyId = company,
                        EmployeeId = employee,
                        SourceId = sale.MerchandiseOrderId,
                        SourceCode = sale.ExternalId,
                        SourceType = "MerchandiseOrder",
                        EventType = EventType.MerchadiseStatus,
                        Status = sale.Status,
                        CreatedDate = now,
                        Note = $"Cập nhật bởi hệ thống vào {now} bởi {user.UserName}"
                    }, ct);
                }
            }

            if (command.DeliveryPromises is { } promises)
            {
                if (deliverySchedules is null) return OperationResult.Fail("Không thể lưu ngày hứa cùng yêu cầu xuất kho.");
                var saved = await deliverySchedules.SaveManufacturingPromisesAsync(command.Id, promises, transaction.TransactionId, ct);
                if (!saved.Success) return saved;
            }
            await db.SaveChangesAsync(ct);
            await promisedDelivery.PublishAsync(order.MfgProductionOrderId, order.ExternalId, previousExpectedDate, order.ExpectedDate, ct);
            await transaction.CommitAsync(ct);
            return OperationResult.Ok("Cập nhật lệnh dùng hàng tồn thành công.");
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return OperationResult.Fail("Không thể cập nhật lệnh dùng hàng tồn.");
        }
    }
}
