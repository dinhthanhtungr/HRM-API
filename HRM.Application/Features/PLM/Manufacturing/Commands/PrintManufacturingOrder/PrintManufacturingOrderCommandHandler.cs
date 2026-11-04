using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.PrintManufacturingOrder;

internal sealed class PrintManufacturingOrderCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IPLMFieldVisibilityService visibility,
    IManufacturingOrderPdfRenderer renderer
) : IRequestHandler<PrintManufacturingOrderCommand, OperationResult<byte[]>>
{
    public async Task<OperationResult<byte[]>> Handle(PrintManufacturingOrderCommand request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders, true);
        if (error is not null || !visibility.CanViewFormulaMaterials())
        {
            return Fail(error ?? "Không có quyền xem vật tư công thức.");
        } // Legacy print persists inferred lots. A read-only capability cannot authorize this side effect.

        if (!permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders)
            && !permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingFormulas))
        {
            return Fail("Không có quyền lưu lot khi in lệnh.");
        }

        if (request.OrderId == Guid.Empty)
        {
            return Fail("Lệnh sản xuất không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        await using var tx = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockProductionOrderAsync(request.OrderId, company, ct);
            var order = await db.MfgProductionOrders.FirstOrDefaultAsync(x => x.MfgProductionOrderId == request.OrderId && x.CompanyId == company && x.IsActive, ct);
            if (order is null)
            {
                return Fail("Không tìm thấy lệnh sản xuất active trong công ty.");
            }

            var formulaId = await db.ProductionSelectVersions.AsNoTracking().Where(x => x.MfgProductionOrderId == request.OrderId && x.CompanyId == company && x.ValidTo == null)
                .OrderByDescending(x => x.ValidFrom)
                .ThenByDescending(x => x.ProductionSelectVersionId)
                .Select(x => (Guid? )x.ManufacturingFormulaId)
                .FirstOrDefaultAsync(ct);
            if (formulaId is null)
            {
                return Fail("Lệnh chưa có VA hiện hành.");
            }

            await db.LockManufacturingFormulaAsync(formulaId.Value, company, ct);
            var formula = await db.ManufacturingFormulas.FirstOrDefaultAsync(x => x.ManufacturingFormulaId == formulaId && x.CompanyId == company && x.IsActive, ct);
            if (formula is null)
            {
                return Fail("Không tìm thấy VA active trong công ty.");
            }

            var materials = await db.ManufacturingFormulaMaterials.AsNoTracking().Where(x => x.ManufacturingFormulaId == formulaId && x.ManufacturingFormula.CompanyId == company && x.IsActive)
                .OrderBy(x => x.LineNo == 0 ? int.MaxValue : x.LineNo)
                .ThenBy(x => x.MaterialExternalIdSnapshot)
                .Select(x => new ManufacturingOrderPdfMaterial
            {
                LineNo = x.LineNo,
                CategoryId = x.CategoryId,
                Quantity = x.Quantity,
                LotNo = x.LotNo,
                MaterialName = x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.Material != null && x.Material.CompanyId == company ? x.Material.Name ?? "" : x.MaterialNameSnapshot ?? "" : x.Product != null && x.Product.CompanyId == company ? (string.IsNullOrWhiteSpace(x.Product.ColourCode) ? x.Product.Name ?? "" : "[" + x.Product.ColourCode + "] " + (x.Product.Name ?? "")) : x.MaterialNameSnapshot ?? "",
                ExternalId = x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.Material != null && x.Material.CompanyId == company ? x.Material.ExternalId ?? "" : x.MaterialExternalIdSnapshot ?? "" : x.Product != null && x.Product.CompanyId == company ? x.Product.SampleRequests.Where(s => s.IsActive && s.CompanyId == company).OrderByDescending(s => s.CreatedDate)
                    .Select(s => s.ExternalId)
                    .FirstOrDefault() ?? x.MaterialExternalIdSnapshot ?? "" : x.MaterialExternalIdSnapshot ?? ""
            })
                .ToListAsync(ct);
            var missingCodes = materials.Where(x => string.IsNullOrWhiteSpace(x.LotNo) && !string.IsNullOrWhiteSpace(x.ExternalId))
                .Select(x => x.ExternalId.Trim().ToUpperInvariant())
                .Distinct()
                .ToArray();
            var stocks = await db.WarehouseShelfStocks.AsNoTracking().Where(x => x.CompanyId == company && missingCodes.Contains(x.Code.ToUpper()) && x.WarehouseShelves != null
                && x.WarehouseShelves.CompanyId == company
                && x.WarehouseShelves.IsActive
                && x.StockType == StockType.RawMaterial
                && x.QtyKg > 0
                && x.LotNo != null
                && x.LotNo != "")
                .Select(x => new { x.Code, x.LotNo, x.UpdatedDate, x.ShelfStockId })
                .ToListAsync(ct);
            var lotMap = stocks.GroupBy(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(s => s.UpdatedDate).ThenByDescending(s => s.ShelfStockId).First().LotNo!.Trim(),
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var material in materials.Where(x => string.IsNullOrWhiteSpace(x.LotNo)))
            {
                if (lotMap.TryGetValue(material.ExternalId.Trim(), out var lot))
                {
                    material.LotNo = lot;
                }
            }

            var savedRows = await db.ManufacturingFormulaMaterials.Where(x => x.ManufacturingFormulaId == formulaId && x.IsActive && x.ManufacturingFormula.CompanyId == company)
                .Select(x => new { Entity = x, Code = !string.IsNullOrWhiteSpace(x.MaterialExternalIdSnapshot) ? x.MaterialExternalIdSnapshot : (x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure) && x.Material != null
                && x.Material.CompanyId == company ? x.Material.ExternalId : null })
                .ToListAsync(ct);
            var changed = false;
            foreach (var row in savedRows.Where(x => string.IsNullOrWhiteSpace(x.Entity.LotNo) && !string.IsNullOrWhiteSpace(x.Code)))
            {
                if (lotMap.TryGetValue(row.Code!.Trim(), out var lot))
                {
                    row.Entity.LotNo = lot;
                    changed = true;
                }
            }

            var copied = formula.SourceType == FormulaSource.FromVA ? formula.SourceManufacturingExternalIdSnapshot : formula.SourceVUExternalIdSnapshot;
            var histories = await (
                from v in db.ProductionSelectVersions.AsNoTracking()join o in db.MfgProductionOrders.AsNoTracking()on v.MfgProductionOrderId equals o.MfgProductionOrderId
                join f in db.ManufacturingFormulas.AsNoTracking()on v.ManufacturingFormulaId equals f.ManufacturingFormulaId
                where v.CompanyId == company && o.CompanyId == company && f.CompanyId == company
                && o.ProductId == order.ProductId
                && f.ManufacturingFormulaId != formulaId
                && v.ValidFrom < formula.CreatedDate
                select new
                {
                    f.ExternalId,
                    v.ValidFrom
                }

            ).ToListAsync(ct);
            var history = string.Join("    ", histories.Where(x => !string.IsNullOrWhiteSpace(x.ExternalId)
                && !string.Equals(x.ExternalId.Trim(), formula.ExternalId.Trim(), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.ExternalId.Trim(), copied?.Trim(), StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.ExternalId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.OrderByDescending(v => v.ValidFrom).First())
                .OrderByDescending(x => x.ValidFrom)
                .Take(4)
                .Select(x => x.ExternalId.Trim()));
            var sale = await db.MfgOrderPOs.AsNoTracking().Where(x => x.MfgProductionOrderId == request.OrderId && x.IsActive && x.Detail.IsActive
                && x.Detail.MerchandiseOrder.CompanyId == company
                && x.Detail.MerchandiseOrder.IsActive)
                .Select(x => new { x.Detail.MerchandiseOrder.ExternalId, x.Detail.MerchandiseOrder.PONo })
                .FirstOrDefaultAsync(ct);
            var usage = await db.Products.AsNoTracking().Where(x => x.ProductId == order.ProductId && x.CompanyId == company)
                .Select(x => x.UsageRate)
                .FirstOrDefaultAsync(ct);
            var document = new ManufacturingOrderPdfDocument

            {

                BatchNo = order.ExternalId,

                RequestDate = order.RequiredDate,

                BagType = order.BagType ?? "",

                CreatedDate = order.CreatedDate,

                CopiedFromCode = copied,

                FormulaSelectList = history,

                Materials = materials,

                Header = new()

                {

                    FormulaExternalId = formula.ExternalId,

                    TotalProductionQuantity = order.TotalQuantity ?? 0,

                    NumOfBatches = order.NumOfBatches,

                    UsageRate = (double)(usage ?? 0),

                    QcCheck = order.QcCheck,

                    ColourCode = order.ProductExternalIdSnapshot,

                    Name = order.ProductNameSnapshot,

                    CustomerCode = order.CustomerExternalIdSnapshot,

                    MerchandiseOrderExternalId = sale?.ExternalId,

                    PONo = sale?.PONo,

                    LabNote = order.LabNote,

                    Requirement = order.Requirement,

                    PlpuNote = order.PlpuNote

                }

            };
            var bytes = renderer.Render(document);
            if (bytes.Length == 0)
            {
                return Fail("Không thể tạo PDF lệnh sản xuất.");
            }

            if (changed)
            {
                await db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
            return OperationResult<byte[]>.Ok(bytes);
        }
        catch (Exception ex)when (ex is not OperationCanceledException)
        {
            await tx.RollbackAsync(ct);
            return Fail("Không thể tạo PDF lệnh sản xuất.");
        }
    }

    private static OperationResult<byte[]> Fail(string message) => OperationResult<byte[]>.Fail(message);
}
