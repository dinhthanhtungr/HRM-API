using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;

internal static class ManufacturingInformationActionRules
{
    // The old information form gates buttons through StatusSets.MPO. Move that
    // decision to the server for the new action contract, including direct API calls.
    public static bool CanApply(string? status, ManufacturingInformationAction action)
    {
        if (!Enum.TryParse<ManufacturingProductOrder>(status, false, out var parsed) || !Enum.IsDefined(parsed)
            || parsed is ManufacturingProductOrder.Unknown or ManufacturingProductOrder.WaitingForStandardSample)
        {
            return false;
        }

        return action switch
        {
            ManufacturingInformationAction.UseStock => parsed == ManufacturingProductOrder.New,
            ManufacturingInformationAction.RequestFormula => parsed is ManufacturingProductOrder.New or ManufacturingProductOrder.FormulaSuccess,
            ManufacturingInformationAction.Schedule => parsed == ManufacturingProductOrder.FormulaSuccess,
            ManufacturingInformationAction.SavePlan => parsed is not (ManufacturingProductOrder.New or ManufacturingProductOrder.FormulaSuccess),
            _ => false
        };
    }

    public static UpdateProductionOrderInformationRequest Prepare(
        ManufacturingInformationAction action,
        UpdateProductionOrderInformationRequest input,
        DateTime now
    ) => new()

    {

        StepOfProduct = input.StepOfProduct,

        ExpectedDate = action == ManufacturingInformationAction.SavePlan ? input.ExpectedDate ?? now : input.ExpectedDate,

        TotalQuantity = input.TotalQuantity,

        NumOfBatches = input.NumOfBatches,

        LabNote = input.LabNote,

        Requirement = input.Requirement,

        QcCheck = input.QcCheck,

        PlpuNote = action == ManufacturingInformationAction.UseStock ? input.PlpuNote ?? "Xuất tồn kho" : input.PlpuNote,

        Status = action switch
        {
            ManufacturingInformationAction.Schedule => ManufacturingProductOrder.Scheduling.ToString(),
            ManufacturingInformationAction.RequestFormula => ManufacturingProductOrder.FormulaRequested.ToString(),
            ManufacturingInformationAction.UseStock => ManufacturingProductOrder.Stocked.ToString(),
            _ => null
        }

    };
    public static async Task<OperationResult> ValidateAsync(
        IManufacturingDbContext db,
        MfgProductionOrder order,
        ManufacturingInformationAction action,
        UpdateProductionOrderInformationRequest input,
        CancellationToken ct
    )
    {
        if (!Enum.IsDefined(action))
        {
            return OperationResult.Fail("Thao tác không hợp lệ.");
        }

        if (!CanApply(order.Status, action))
        {
            return OperationResult.Fail("Thao tác không phù hợp trạng thái hiện tại của lệnh. Vui lòng tải lại dữ liệu.");
        }

        if (action is ManufacturingInformationAction.Schedule or ManufacturingInformationAction.SavePlan
            && (input.ExpectedDate is null || !(input.TotalQuantity > 0) || !(input.NumOfBatches > 0)
            || input.StepOfProduct is null
            || !Enum.IsDefined(input.StepOfProduct.Value)))
        {
            return OperationResult.Fail("Ngày dự kiến, khối lượng, số mẻ và công đoạn sản xuất không hợp lệ.");
        }

        if ((action == ManufacturingInformationAction.UseStock
            || order.Status == ManufacturingProductOrder.Stocked.ToString())
            && !(input.TotalQuantity > 0))
        {
            return OperationResult.Fail("Lượng dùng tồn phải lớn hơn 0.");
        }

        if (action == ManufacturingInformationAction.Schedule
            || action == ManufacturingInformationAction.SavePlan
            && order.Status != ManufacturingProductOrder.Stocked.ToString())
        {
            var selection = await SelectedAsync(db, order, ct);
            if (selection is null)
            {
                return OperationResult.Fail("Lệnh chưa có VA active đang được chọn.");
            }
        }

        return OperationResult.Ok();
    }

    public static async Task<OperationResult> ReserveAsync(
        IManufacturingDbContext db,
        ProductionOrderReservationService reservations,
        MfgProductionOrder order,
        DateTime now,
        CancellationToken ct
    )
    {
        var selected = await SelectedAsync(db, order, ct);
        if (selected is null)
        {
            return OperationResult.Fail("Lệnh chưa có VA active đang được chọn.");
        }

        await db.LockManufacturingFormulaAsync(selected.Value, order.CompanyId, ct);
        var rows = await db.ManufacturingFormulaMaterials.AsNoTracking().Where(x => x.ManufacturingFormulaId == selected && x.IsActive
            && x.ManufacturingFormula.CompanyId == order.CompanyId
            && x.ManufacturingFormula.IsActive)
            .Select(x => new CreateProductionOrderFormulaItemRequest
        {
            ItemId = x.MaterialId ?? x.ProductId ?? Guid.Empty,
            ItemType = x.itemType,
            Quantity = x.Quantity,
            IsActive = true,
            MaterialExternalIdSnapshot = x.Material != null && x.Material.CompanyId == order.CompanyId ? x.Material.ExternalId : x.MaterialExternalIdSnapshot
        })
            .ToListAsync(ct);
        return await reservations.SyncAsync(order, rows, now, ct);
    }

    private static Task<Guid?> SelectedAsync(
        IManufacturingDbContext db,
        MfgProductionOrder order,
        CancellationToken ct
    ) => db.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == order.CompanyId && x.MfgProductionOrderId == order.MfgProductionOrderId
        && x.ValidTo == null
        && x.ManufacturingFormula != null
        && x.ManufacturingFormula.CompanyId == order.CompanyId
        && x.ManufacturingFormula.IsActive)
        .OrderByDescending(x => x.ValidFrom)
        .ThenByDescending(x => x.ProductionSelectVersionId)
        .Select(x => (Guid? )x.ManufacturingFormulaId)
        .FirstOrDefaultAsync(ct);
}
