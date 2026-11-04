using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal sealed class ManufacturingAdjustmentInputResolver(IManufacturingDbContext db)
{
    public async Task<OperationResult<Dictionary<(bool Material, Guid Id, string Lot), ManufacturingFormulaAdjustmentItem>>> ResolveAsync(
        Guid company,
        IReadOnlyList<ManufacturingAdjustmentRangeInput> ranges,
        CancellationToken ct
    )
    {
        var rows = ranges.SelectMany(x => x.Items).ToList();
        var materialIds = rows.Where(x => ManufacturingAdjustmentBatchRules.IsMaterial(x.ItemType)).Select(x => x.ItemId)
            .Distinct()
            .ToArray();
        var productIds = rows.Where(x => !ManufacturingAdjustmentBatchRules.IsMaterial(x.ItemType)).Select(x => x.ItemId)
            .Distinct()
            .ToArray();
        var materials = await db.Materials.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive == true && materialIds.Contains(x.MaterialId))
            .Select(x => new Item(x.MaterialId, x.CategoryId, x.Name, x.ExternalId, x.ExternalId))
            .ToDictionaryAsync(x => x.Id, ct);
        var products = await db.Products.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && productIds.Contains(x.ProductId))
            .Select(x => new Item(
            x.ProductId,
            x.CategoryId,
            string.IsNullOrEmpty(x.ColourCode) ? x.Name : "[" + x.ColourCode + "] " + x.Name,
            x.ColourCode,
            x.ColourCode
        ))
            .ToDictionaryAsync(x => x.Id, ct);
        if (materials.Count != materialIds.Length || products.Count != productIds.Length)
        {
            return Fail("Vật tư/sản phẩm không active trong công ty.");
        }

        var categories = materials.Values.Concat(products.Values).Select(x => x.Category).Distinct().ToArray();
        if (await db.Categories.AsNoTracking().CountAsync(x => categories.Contains(x.CategoryId) && x.CompanyId == company && x.IsActive == true, ct) != categories.Length)
        {
            return Fail("Nhóm vật tư không active trong công ty.");
        }

        var codes = materials.Values.Concat(products.Values).Select(x => x.StockCode).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();
        var lotNames = rows.Select(x => x.LotNo?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, "N/A", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();
        var stocks = await db.WarehouseShelfStocks.AsNoTracking().Where(x => x.CompanyId == company && x.WarehouseShelves != null && x.WarehouseShelves.CompanyId == company
            && x.WarehouseShelves.IsActive
            && codes.Contains(x.Code.Trim().ToUpper())
            && lotNames.Contains(x.LotNo))
            .Select(x => new { x.Code, x.LotNo, x.StockType })
            .Distinct()
            .ToListAsync(ct);
        var result = new Dictionary<(bool Material, Guid Id, string Lot), ManufacturingFormulaAdjustmentItem>();
        foreach (var row in rows)
        {
            var material = ManufacturingAdjustmentBatchRules.IsMaterial(row.ItemType);
            var data = (material ? materials : products)[row.ItemId];
            var lot = row.LotNo?.Trim() ?? string.Empty;
            // N/A/blank has no stock row to infer quality from: preserve the selected type.
            var type = row.ItemType;
            if (!string.IsNullOrWhiteSpace(lot) && !string.Equals(lot, "N/A", StringComparison.OrdinalIgnoreCase))
            {
                var matches = stocks.Where(x => x.LotNo == lot
                    && string.Equals(x.Code.Trim(), data.StockCode?.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count != 1)
                {
                    return Fail("Lot vật tư không thuộc công ty hoặc có loại tồn không nhất quán.");
                }

                type = ProductionOrderCreationRules.ResolveItemType(type, new ProductionOrderLotNumberDto
                {
                    LotNo = lot,
                    StockType = matches[0].StockType
                });
            }

            result[(material, row.ItemId, lot)] = new ManufacturingFormulaAdjustmentItem

            {

                MaterialId = material ? row.ItemId : null,

                ProductId = material ? null : row.ItemId,

                CategoryId = data.Category,

                itemType = type,

                MaterialNameSnapshot = material ? data.Name : null,

                MaterialExternalIdSnapshot = material ? data.Code : null,

                ProductNameSnapshot = material ? null : data.Name,

                ProductExternalIdSnapshot = material ? null : data.Code

            };
        }

        return OperationResult<Dictionary<(bool Material, Guid Id, string Lot), ManufacturingFormulaAdjustmentItem>>.Ok(result);
    }

    private sealed record Item(
        Guid Id,
        Guid Category,
        string? Name,
        string? Code,
        string? StockCode
    );
    private static OperationResult<Dictionary<(bool Material, Guid Id, string Lot), ManufacturingFormulaAdjustmentItem>> Fail(string message) => OperationResult<Dictionary<(bool Material, Guid Id, string Lot), ManufacturingFormulaAdjustmentItem>>.Fail(message);
}
