using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInformation;

internal sealed class UpdateProductionOrderInformationCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock,
    IEventLogWriter timeline,
    ProductionOrderReservationService reservations
) : IRequestHandler<UpdateProductionOrderInformationCommand, OperationResult>
{
    public async Task<OperationResult> Handle(UpdateProductionOrderInformationCommand command, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ManageProductionOrders, true);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        var request = command.Request;
        if (request.StepOfProduct.HasValue && !Enum.IsDefined(request.StepOfProduct.Value)
            || request.Status is not null && !Enum.GetNames<ManufacturingProductOrder>().Contains(request.Status))
        {
            return OperationResult.Fail("Trạng thái hoặc bước sản xuất không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockProductionOrderAsync(command.Id, company, ct);
            var link = await db.MfgOrderPOs.Where(x => x.IsActive && x.MfgProductionOrderId == command.Id && x.ProductionOrder.CompanyId == company
                && x.ProductionOrder.IsActive
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
                return OperationResult.Fail("Chỉ PLPUUser, President và Developer được sửa thông tin kế hoạch lệnh.");
            if (order.Status == nameof(ManufacturingProductOrder.WaitingForStandardSample)
                || request.Status == nameof(ManufacturingProductOrder.WaitingForStandardSample))
                return OperationResult.Fail("Lệnh đang chờ Sale xác nhận mẫu chuẩn; chưa được thao tác kế hoạch.");
            error = ManufacturingAccess.ValidatePlpuNoteChange(permissions, order.PlpuNote, request.PlpuNote);
            if (error is not null) return OperationResult.Fail(error);
            error = ManufacturingAccess.ValidateRequirementChange(permissions, order.Requirement, request.Requirement);
            if (error is not null) return OperationResult.Fail(error);
            if (!await ManufacturingEditorRevision.MatchesAsync(db, order, command.ExpectedRevision, ct))
                return OperationResult.Fail(ManufacturingEditorRevision.Conflict);
            var now = clock.Now;
            var previousQuantity = order.TotalQuantity;
            if (command.Action.HasValue)
            {
                if (!link.Detail.IsActive || !link.Detail.MerchandiseOrder.IsActive)
                {
                    return OperationResult.Fail("Liên kết đơn hàng không active.");
                }

                request = ManufacturingInformationActionRules.Prepare(command.Action.Value, request, now);
                var validation = await ManufacturingInformationActionRules.ValidateAsync(db, order, command.Action.Value, request, ct);
                if (!validation.Success)
                {
                    return validation;
                }
            }

            order.UpdatedBy = employee;
            order.UpdatedDate = now;
            order.StepOfProduct = request.StepOfProduct;
            PatchHelper.SetIfNotNull(request.PlpuNote, () => order.PlpuNote, v => order.PlpuNote = v);
            PatchHelper.SetIfNotNull(request.LabNote, () => order.LabNote, v => order.LabNote = v);
            PatchHelper.SetIfNotNull(request.Requirement, () => order.Requirement, v => order.Requirement = v);
            PatchHelper.SetIfNotNull(request.QcCheck, () => order.QcCheck, v => order.QcCheck = v);
            PatchHelper.SetNullable(request.TotalQuantity, () => order.TotalQuantity, v => order.TotalQuantity = v);
            PatchHelper.SetNullable(request.NumOfBatches, () => order.NumOfBatches, v => order.NumOfBatches = v);
            PatchHelper.SetNullable(request.ExpectedDate, () => order.ExpectedDate, v => order.ExpectedDate = v);
            var statusChanged = PatchHelper.SetIfNotNull(request.Status, () => order.Status, v => order.Status = v!);
            if (statusChanged)
            {
                await WriteEvent(
                    order.MfgProductionOrderId,
                    order.ExternalId,
                    order.Status,
                    EventType.ManufacturingProductOrder,
                    now,
                    ct
                );
            }

            var schedule = await db.SchedualMfgs.FirstOrDefaultAsync(x => x.MfgProductionOrderId == command.Id, ct);
            if (statusChanged && order.Status == ManufacturingProductOrder.Scheduling.ToString() && schedule is null)
            {
                await db.SchedualMfgs.AddAsync(new SchedualMfg
                {
                    MfgProductionOrderId = command.Id,
                    ProductId = order.ProductId,
                    requirement = order.Requirement,
                    Note = order.LabNote ?? order.PlpuNote,
                    DeliveryPlanDate = order.ExpectedDate,
                    CreatedDate = now,
                    Status = ManufacturingProductOrder.Scheduling.ToString()
                }, ct);
            }

            if (schedule is not null)
            {
                schedule.DeliveryPlanDate = order.ExpectedDate;
            }

            if (statusChanged)
            {
                var saleOrder = await db.MerchandiseOrders.FirstOrDefaultAsync(x => x.MerchandiseOrderId == link.Detail.MerchandiseOrderId && x.CompanyId == company, ct);
                if (saleOrder is not null)
                {
                    if (!Enum.TryParse<MerchadiseStatus>(saleOrder.Status, true, out var status))
                    {
                        status = MerchadiseStatus.New;
                    }

                    if (status is MerchadiseStatus.New or MerchadiseStatus.Approved)
                    {
                        saleOrder.Status = MerchadiseStatus.Processing.ToString();
                        saleOrder.UpdatedDate = now;
                        saleOrder.UpdatedBy = employee;
                        await WriteEvent(
                            saleOrder.MerchandiseOrderId,
                            saleOrder.ExternalId,
                            saleOrder.Status,
                            EventType.MerchadiseStatus,
                            now,
                            ct
                        );
                    }
                }
            }

            if (command.Action is ManufacturingInformationAction.Schedule or ManufacturingInformationAction.SavePlan
                && order.Status != ManufacturingProductOrder.Stocked.ToString()
                && previousQuantity != order.TotalQuantity)
            {
                var reserved = await ManufacturingInformationActionRules.ReserveAsync(db, reservations, order, now, ct);
                if (!reserved.Success)
                {
                    return reserved;
                }
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult.Ok("Cập nhật thông tin lệnh sản xuất thành công.");
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return OperationResult.Fail("Có lỗi xảy ra trong quá trình cập nhật lệnh sản xuất.");
        }
    }

    private Task WriteEvent(
        Guid id,
        string code,
        string status,
        EventType type,
        DateTime now,
        CancellationToken ct
    ) => timeline.AddAsync(new EventLogCreateRequest
    {
        EmployeeId = user.EmployeeId!.Value,
        CompanyId = user.CompanyId!.Value,
        SourceId = id,
        SourceCode = code,
        SourceType = type == EventType.ManufacturingProductOrder ? "MfgProductionOrder" : "MerchandiseOrder",
        EventType = type,
        Status = status,
        CreatedDate = now,
        Note = $"Cap nhat boi he thong vào {now} boi {user.UserName}"
    }, ct);
}
