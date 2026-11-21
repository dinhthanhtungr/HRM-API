using System.Text.RegularExpressions;
using RequestType = HRM.Domain.Enums.WareHouses.WareHouseRequestType;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Helpers.Publics;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Services;

/// <summary>Đọc chứng từ nguồn theo batch, cùng company; chỉ bổ sung mô tả, không ghi kho.</summary>
internal sealed class WarehouseVoucherBusinessReader(
    IWarehouseReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions) : IWarehouseVoucherBusinessReader
{
    private static readonly Regex AdjustmentSource = new(
        @"(?:^|;|\s)MfgFormulaAdjustment:([0-9a-fA-F-]{36})(?=$|;|\s)", RegexOptions.CultureInvariant);

    public async Task<IReadOnlyDictionary<long, WarehouseVoucherBusinessInfo>> ReadAsync(
        Guid companyId, IReadOnlyCollection<WarehouseVoucherBusinessInput> inputs, CancellationToken ct)
    {
        var canViewSupplier = user.CompanyId == companyId && WarehouseVoucherAccessRules.CanViewSupplier(user, permissions);
        var result = inputs.ToDictionary(x => x.VoucherId,
            x => new WarehouseVoucherBusinessInfo(WarehouseVoucherBusinessTypes.Fallback(x.VoucherType), CanViewSupplier: canViewSupplier));
        if (!user.IsAuthenticated || user.CompanyId != companyId || companyId == Guid.Empty || inputs.Count == 0) return result;
        var ids = inputs.Select(x => x.VoucherId).ToArray();
        var codes = inputs.Select(x => x.SourceCode?.Trim() ?? string.Empty).Where(x => x.Length > 0).Distinct().ToArray();

        var purchases = await db.PurchaseOrders.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ExternalId != null && codes.Contains(x.ExternalId))
            .Select(x => new {
                Code = x.ExternalId!,
                Categories = x.PurchaseOrderDetails.Where(d => d.IsActive)
                    .Select(d => d.Material != null && d.Material.Category != null ? d.Material.Category.Types : null).ToList()
            }).ToListAsync(ct);
        var purchaseMap = purchases.GroupBy(x => x.Code, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        // Supplier snapshots and PO notes are never selected for an unauthorized reader.
        var supplierMap = new Dictionary<string, SupplierInfo>(StringComparer.Ordinal);
        if (canViewSupplier)
        {
            var supplierCodes = purchaseMap.Where(x => x.Value.Count == 1).Select(x => x.Key).ToArray();
            var suppliers = await db.PurchaseOrders.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.ExternalId != null && supplierCodes.Contains(x.ExternalId))
                .Select(x => new
                {
                    Code = x.ExternalId!, x.Comment,
                    Name = x.PurchaseOrderSnapshot != null ? x.PurchaseOrderSnapshot.SupplierNameSnapshot : null,
                    SupplierCode = x.PurchaseOrderSnapshot != null ? x.PurchaseOrderSnapshot.SupplierExternalIdSnapshot : null
                }).ToListAsync(ct);
            supplierMap = suppliers.ToDictionary(x => x.Code,
                x => new SupplierInfo(x.Name ?? "", x.SupplierCode ?? "", x.Comment ?? ""), StringComparer.Ordinal);
        }
        var deliveries = await db.DeliveryOrders.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ExternalId != null && codes.Contains(x.ExternalId))
            .Select(x => x.ExternalId!).ToListAsync(ct);
        var adjustmentIds = inputs.Select(x => AdjustmentId(x.SourceCode)).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var adjustments = await db.ManufacturingFormulaAdjustments.AsNoTracking()
            .Where(x => x.CompanyId == companyId && adjustmentIds.Contains(x.ManufacturingFormulaAdjustmentId))
            .Select(x => x.ManufacturingFormulaAdjustmentId).ToListAsync(ct);
        var productionReceipts = await db.ProductionOutputReceiptLedgers.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.VoucherId != null && ids.Contains(x.VoucherId.Value)
                && x.ProductionOutput.CompanyId == companyId)
            .Select(x => x.VoucherId!.Value).Distinct().ToListAsync(ct);
        var operationIssues = await db.OperationMaterialBuffers.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.VoucherId != null && ids.Contains(x.VoucherId.Value))
            .Select(x => x.VoucherId!.Value).Distinct().ToListAsync(ct);
        var mixingMovements = await (
            from movement in db.WarehouseShelfLedgers.AsNoTracking()
            join detail in db.WarehouseVoucherDetails.AsNoTracking()
                on movement.VoucherDetailId equals (long?)detail.VoucherDetailId into detailJoin
            from detail in detailJoin.DefaultIfEmpty()
            let voucherId = movement.VoucherId ?? (detail != null ? (long?)detail.VoucherId : null)
            where movement.CompanyId == companyId && voucherId != null && ids.Contains(voucherId.Value)
                && (movement.VoucherId == null || detail == null || movement.VoucherId == detail.VoucherId)
                && movement.Shelf.CompanyId == companyId
                && movement.Shelf.SlotCode == WarehouseStockQueryHelper.MixingShelfCode
            select new { Id = voucherId.Value, movement.DeltaKg }).ToListAsync(ct);
        var mixingMap = mixingMovements.Where(x => x.DeltaKg != 0).GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Select(x => x.DeltaKg).ToArray());

        foreach (var input in inputs)
        {
            var type = (RequestType)input.VoucherType;
            var source = input.SourceCode?.Trim() ?? string.Empty;
            if (type is RequestType.ImportRawMaterial or RequestType.ImportOther or RequestType.ImportMaterial or RequestType.ExportReturn && purchaseMap.TryGetValue(source, out var matches) && matches.Count == 1)
            {
                var po = matches[0];
                var categories = po.Categories.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                var raw = categories.Any(x => string.Equals(x, "NVL", StringComparison.OrdinalIgnoreCase));
                var supplies = categories.Any(x => !string.Equals(x, "NVL", StringComparison.OrdinalIgnoreCase));
                var business = type == RequestType.ExportReturn
                    ? WarehouseVoucherBusinessTypes.Create("PurchaseReturn", "Xuất trả nhà cung cấp", "Return to supplier")
                    : raw && supplies
                        ? WarehouseVoucherBusinessTypes.Create("PurchaseGoodsReceipt", "Nhập hàng mua từ NCC", "Purchased goods receipt")
                        : supplies || type == RequestType.ImportMaterial
                            ? WarehouseVoucherBusinessTypes.Create("PurchaseSuppliesReceipt", "Nhập vật tư mua từ NCC", "Purchased supplies receipt")
                            : raw || type == RequestType.ImportRawMaterial
                                ? WarehouseVoucherBusinessTypes.Create("PurchaseRawMaterialReceipt", "Nhập NVL mua từ NCC", "Purchased raw material receipt")
                                : WarehouseVoucherBusinessTypes.Create("PurchaseGoodsReceipt", "Nhập hàng mua từ NCC", "Purchased goods receipt");
                supplierMap.TryGetValue(po.Code, out var supplier);
                result[input.VoucherId] = new(business, po.Code,
                    supplier?.Name, supplier?.Code, supplier?.Comments, canViewSupplier);
                continue;
            }
            if (type is RequestType.ExportFinishedGood or RequestType.ExportDefectiveFinishedGood && deliveries.Contains(source, StringComparer.Ordinal))
            {
                result[input.VoucherId] = new(type == RequestType.ExportDefectiveFinishedGood
                    ? WarehouseVoucherBusinessTypes.Create("DeliveryDefectiveGoodsIssue", "Xuất thành phẩm lỗi giao khách", "Defective goods delivery issue")
                    : WarehouseVoucherBusinessTypes.Create("DeliveryGoodsIssue", "Xuất thành phẩm giao khách", "Customer delivery issue"));
                continue;
            }
            var adjustment = AdjustmentId(source);
            if (type == RequestType.ExportOther && adjustment.HasValue && adjustments.Contains(adjustment.Value))
            {
                result[input.VoucherId] = new(WarehouseVoucherBusinessTypes.Create("ColorAdjustmentIssue", "Xuất bổ sung chỉnh màu", "Color adjustment material issue"));
                continue;
            }
            if (type is RequestType.ImportFinishedGood or RequestType.ImportDefectiveFinishedGood && productionReceipts.Contains(input.VoucherId))
            {
                result[input.VoucherId] = new(type == RequestType.ImportDefectiveFinishedGood
                    ? WarehouseVoucherBusinessTypes.Create("ProductionDefectiveReceipt", "Nhập thành phẩm lỗi sản xuất", "Production defective goods receipt")
                    : WarehouseVoucherBusinessTypes.Create("ProductionReceipt", "Nhập thành phẩm sản xuất", "Production finished goods receipt"));
                continue;
            }
            if (mixingMap.TryGetValue(input.VoucherId, out var deltas))
            {
                if (deltas.All(x => x > 0))
                    result[input.VoucherId] = new(WarehouseVoucherBusinessTypes.Create("MixingReceipt", "Nhập kho cân trộn", "Mixing warehouse receipt"));
                else if (deltas.All(x => x < 0))
                    result[input.VoucherId] = new(operationIssues.Contains(input.VoucherId)
                        ? WarehouseVoucherBusinessTypes.Create("MixingProductionIssue", "Xuất từ cân trộn cho sản xuất", "Mixing warehouse production issue")
                        : WarehouseVoucherBusinessTypes.Create("MixingIssue", "Xuất kho cân trộn", "Mixing warehouse issue"));
            }
            else if (type is RequestType.ExportRawMaterial or RequestType.ExportDefectiveRawMaterial or RequestType.ExportOther or RequestType.ExportMaterial && operationIssues.Contains(input.VoucherId))
                result[input.VoucherId] = new(WarehouseVoucherBusinessTypes.Create("ProductionMaterialIssue", "Xuất cấp sản xuất", "Production material issue"));
        }
        return result.ToDictionary(x => x.Key, x => x.Value with { CanViewSupplier = canViewSupplier });
    }

    private static Guid? AdjustmentId(string? source)
    {
        var match = AdjustmentSource.Match(source ?? string.Empty);
        return match.Success && Guid.TryParse(match.Groups[1].Value, out var id) ? id : null;
    }

    private sealed record SupplierInfo(string Name, string Code, string Comments);
}
