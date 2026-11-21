using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

public sealed class ProcessingWarehouseService(IManufacturingDbContext db, IMaterialPriceQueryService prices,
    IDateTimeProvider clock, ICurrentUserPermissionService permissions)
{
    public Task<string?> FormulaEditLockAsync(MfgProductionOrder order, CancellationToken ct)
        => HRM.Application.Features.PLM.Manufacturing.Services.Access.ManufacturingAccess.FormulaEditLockAsync(db, order, ct);

    public async Task LockAsync(Guid id, Guid company, CancellationToken ct)
    {
        await db.LockProductionOrderAsync(id, company, ct);
        var formulaId = await db.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == company
            && x.MfgProductionOrderId == id && x.ValidTo == null).Select(x => x.ManufacturingFormulaId).FirstOrDefaultAsync(ct);
        if (formulaId.HasValue) await db.LockManufacturingFormulaAsync(formulaId.Value, company, ct);
    }
    public async Task<ProcessingStockDto?> StockAsync(Guid materialId, int itemType, Guid? orderId, ICurrentUser user, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty
            || !permissions.HasPermission(ApplicationPermissions.PLM.ViewMaterialProcessing)
            || !permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials)
            || itemType is not (0 or 2) || itemType == 2 && !permissions.HasPermission(ApplicationPermissions.PLM.ViewManufacturingDefectiveLots)) return null;
        var material = await db.Materials.AsNoTracking().FirstOrDefaultAsync(x => x.MaterialId == materialId && x.CompanyId == company && x.IsActive == true, ct);
        if (material is null) return null;
        if (orderId.HasValue && !await SemiFinishedProcessing.Orders(db, company)
            .AnyAsync(x => x.MfgProductionOrderId == orderId, ct)) return null;
        var sources = await ManufacturingFormulaStockReader.FillAsync(db, company,
            [new(Guid.Empty, materialId, (ItemType)itemType, material.CategoryId, 1, 1, null, material.Name, material.ExternalId, material.Unit, null)],
            ct, canViewDefectiveLots: itemType == 2);
        var source = sources.Single();
        var lots = source.Lots.Where(x => x.IsDefective == (itemType == 2) && x.QuantityKg > 0 && x.LotNo != "N/A")
            .Select(x => new ProcessingStockLotDto(x.LotNo, decimal.Truncate(x.QuantityKg * 1000) / 1000))
            .Where(x => x.QuantityKg > 0).ToArray();
        // Internal processing is limited by physical shelf/lot stock, not reservations.
        var available = lots.Sum(x => x.QuantityKg);
        return new(decimal.Truncate(available * 1000) / 1000, lots);
    }
    public async Task<(string? Error, List<CreateProductionOrderFormulaItemRequest>? Items)> ResolveAsync(
        MfgProductionOrder order, ManufacturingFormula? formula, SaveMaterialProcessingRequest input,
        decimal[] ratios, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials)) return ("Bạn không có quyền xem vật tư công thức.", null);
        var source = new ProductionOrderFormulaOptionDto(formula?.ManufacturingFormulaId ?? Guid.Empty,
            formula?.ExternalId ?? "", ManufacturingFormulaOrigin.Production, null, clock.Now, false);
        var rows = input.Details.Select((x, i) => new ManufacturingFormulaInputItem {
            ItemId = x.MaterialId, ItemType = x.ItemType, Quantity = ratios[i], LotNo = x.LotNo,
            SourceItemId = formula?.ManufacturingFormulaMaterials.FirstOrDefault(m => m.IsActive && m.MaterialId == x.MaterialId
                && m.itemType == x.ItemType && string.Equals(m.LotNo?.Trim() ?? "", x.LotNo?.Trim() ?? "", StringComparison.OrdinalIgnoreCase))?.ManufacturingFormulaMaterialId
        }).ToList();
        var resolved = await new ManufacturingFormulaInputResolver(db, prices, clock, permissions).ResolveAsync(
            order, source, rows, ct, refreshExistingPrices: true, allowUntrackedSourceRows: true,
            preserveUnspecifiedLotQuality: true);
        if (!resolved.Success || resolved.Data is null) return (resolved.Message, null);
        var aligned = new List<CreateProductionOrderFormulaItemRequest>();
        foreach (var row in input.Details)
        {
            static string Lot(string? value) => string.IsNullOrWhiteSpace(value) || value.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase) ? "" : value.Trim();
            var matches = resolved.Data.Where(x => x.ItemId == row.MaterialId
                && string.Equals(Lot(x.LotNumber?.LotNo), Lot(row.LotNo), StringComparison.OrdinalIgnoreCase)).ToList();
            var item = matches.SingleOrDefault(x => x.ItemType == row.ItemType) ?? matches.SingleOrDefault();
            if (item is null) return ($"NVL dòng {aligned.Count + 1}: không đối chiếu được dòng công thức.", null);
            if (Lot(row.LotNo).Length > 0 && item.ItemType != row.ItemType)
                return ($"NVL dòng {aligned.Count + 1}: lot không khớp loại hàng đã chọn.", null);
            item.ItemType = row.ItemType;
            aligned.Add(item);
        }
        return (null, aligned);
    }

    public async Task<string?> ReserveAsync(MfgProductionOrder order, List<CreateProductionOrderFormulaItemRequest> items, DateTime now, CancellationToken ct)
    {
        var result = await new ProductionOrderReservationService(db).SyncAsync(order, items, now, ct);
        return result.Success ? null : result.Message;
    }

    private async Task ReleaseReservationsAsync(MfgProductionOrder order, CancellationToken ct)
    {
        var reserves = await db.WarehouseTempStocks.Where(x => x.CompanyId == order.CompanyId
            && x.VaCode == order.ExternalId && x.ReserveStatus == ProductionOrderReservationService.OpenStatus).ToListAsync(ct);
        foreach (var row in reserves) { row.ReserveStatus = ProductionOrderReservationService.CancelledStatus; row.QtyRequest = 0; }
    }

    public async Task CancelAsync(MfgProductionOrder order, CancellationToken ct)
    {
        await ReleaseReservationsAsync(order, ct);
        var schedules = await db.SchedualMfgs.Where(x => x.MfgProductionOrderId == order.MfgProductionOrderId
            && x.MfgProductionOrder!.CompanyId == order.CompanyId).ToListAsync(ct);
        db.SchedualMfgs.RemoveRange(schedules);
    }
}
