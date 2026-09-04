using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Commons.Reporting;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.ExecutivePnL.Shared.Services.Costing;

/// <summary>
/// Tính giá vốn delivery theo nguồn được chọn bởi ExecutivePnLCostSourcePolicy.
/// </summary>
internal static class ExecutivePnLDeliveryCostResolver
{
    public static async Task<ExecutivePnLDeliveryCostSources> LoadAsync(
        IReportReadDbContext dbContext,
        IReadOnlyCollection<DeliveryRevenueLine> revenueLines,
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        var formulaIds = revenueLines
            .Where(x => x.FormulaId != Guid.Empty)
            .Select(x => x.FormulaId)
            .Distinct()
            .ToArray();
        var formulaUnitCostById = await dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x => formulaIds.Contains(x.FormulaId) && x.IsActive && x.itemType == ItemType.Material)
            .GroupBy(x => x.FormulaId)
            .Select(x => new
            {
                FormulaId = x.Key,
                UnitCost = x.Sum(material => material.Quantity * material.UnitPrice)
            })
            .ToDictionaryAsync(x => x.FormulaId, x => x.UnitCost, cancellationToken);

        if (ExecutivePnLCostSourcePolicy.Current == ExecutivePnLCostSource.FormulaSnapshot)
        {
            return new ExecutivePnLDeliveryCostSources(formulaUnitCostById, new Dictionary<Guid, decimal>(), new Dictionary<ExecutivePnLFormulaMonthKey, decimal>());
        }

        if (ExecutivePnLCostSourcePolicy.Current == ExecutivePnLCostSource.InventoryWeightedAverage)
        {
            var weightedAverageUnitCostByFormulaMonth = await LoadInventoryWeightedAverageUnitCostsAsync(
                dbContext,
                formulaIds,
                revenueLines,
                companyId,
                cancellationToken);
            return new ExecutivePnLDeliveryCostSources(formulaUnitCostById, new Dictionary<Guid, decimal>(), weightedAverageUnitCostByFormulaMonth);
        }

        var orderDetailIds = revenueLines
            .Where(x => x.MerchandiseOrderDetailId != Guid.Empty)
            .Select(x => x.MerchandiseOrderDetailId)
            .Distinct()
            .ToArray();
        if (orderDetailIds.Length == 0)
        {
            return new ExecutivePnLDeliveryCostSources(formulaUnitCostById, new Dictionary<Guid, decimal>(), new Dictionary<ExecutivePnLFormulaMonthKey, decimal>());
        }

        var mfgLinks = await dbContext.MfgOrderPOs
            .AsNoTracking()
            .Where(x => x.IsActive && orderDetailIds.Contains(x.MerchandiseOrderDetailId))
            .Where(x => !companyId.HasValue || x.ProductionOrder.CompanyId == companyId.Value)
            .Select(x => new { x.MerchandiseOrderDetailId, x.MfgProductionOrderId })
            .ToListAsync(cancellationToken);
        var mfgIds = mfgLinks.Select(x => x.MfgProductionOrderId).Distinct().ToArray();
        if (mfgIds.Length == 0)
        {
            return new ExecutivePnLDeliveryCostSources(formulaUnitCostById, new Dictionary<Guid, decimal>(), new Dictionary<ExecutivePnLFormulaMonthKey, decimal>());
        }

        var productionQtyByMfg = await dbContext.ProductionOutputReceiptSources
            .AsNoTracking()
            .Where(x => x.MfgProductionOrderId.HasValue && mfgIds.Contains(x.MfgProductionOrderId.Value))
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.ProducedQtyKg > 0m)
            .GroupBy(x => x.MfgProductionOrderId!.Value)
            .Select(x => new { MfgProductionOrderId = x.Key, Quantity = x.Sum(output => output.ProducedQtyKg) })
            .ToDictionaryAsync(x => x.MfgProductionOrderId, x => x.Quantity, cancellationToken);
        var bufferMaterialCostByMfg = await dbContext.OperationMaterialBuffers
            .AsNoTracking()
            .Where(x => x.MfgProductionOrderId.HasValue && mfgIds.Contains(x.MfgProductionOrderId.Value))
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.AmountCost.HasValue && x.AmountCost.Value > 0m)
            .GroupBy(x => x.MfgProductionOrderId!.Value)
            .Select(x => new { MfgProductionOrderId = x.Key, Amount = x.Sum(buffer => buffer.AmountCost!.Value) })
            .ToDictionaryAsync(x => x.MfgProductionOrderId, x => x.Amount, cancellationToken);
        var materialCostByMfg = bufferMaterialCostByMfg;
        var actualUnitCostByMfg = materialCostByMfg
            .Where(x => productionQtyByMfg.TryGetValue(x.Key, out var quantity) && quantity > 0m)
            .ToDictionary(x => x.Key, x => x.Value / productionQtyByMfg[x.Key]);
        var actualUnitCostByOrderDetail = mfgLinks
            .GroupBy(x => x.MerchandiseOrderDetailId)
            .Select(x => new
            {
                MerchandiseOrderDetailId = x.Key,
                MfgProductionOrderIds = x.Select(link => link.MfgProductionOrderId).Distinct().ToArray()
            })
            .Where(x => x.MfgProductionOrderIds.Length == 1 && actualUnitCostByMfg.ContainsKey(x.MfgProductionOrderIds[0]))
            .ToDictionary(
                x => x.MerchandiseOrderDetailId,
                x => actualUnitCostByMfg[x.MfgProductionOrderIds[0]]);

        return new ExecutivePnLDeliveryCostSources(formulaUnitCostById, actualUnitCostByOrderDetail, new Dictionary<ExecutivePnLFormulaMonthKey, decimal>());
    }

    public static decimal ResolveAmount(
        DeliveryRevenueLine line,
        ExecutivePnLDeliveryCostSources sources)
    {
        if (ExecutivePnLCostSourcePolicy.Current == ExecutivePnLCostSource.WarehouseActual &&
            sources.ActualWarehouseUnitCostByOrderDetail.TryGetValue(
                line.MerchandiseOrderDetailId,
                out var actualWarehouseUnitCost))
        {
            return line.Quantity * actualWarehouseUnitCost;
        }

        if (ExecutivePnLCostSourcePolicy.Current == ExecutivePnLCostSource.InventoryWeightedAverage &&
            sources.InventoryWeightedAverageUnitCostByFormulaMonth.TryGetValue(
                ExecutivePnLFormulaMonthKey.From(line),
                out var inventoryWeightedAverageUnitCost))
        {
            return line.Quantity * inventoryWeightedAverageUnitCost;
        }

        if (sources.FormulaUnitCostById.TryGetValue(line.FormulaId, out var formulaUnitCost))
        {
            return line.Quantity * formulaUnitCost;
        }

        return ExecutivePnLDeliveryCostRules.ResolveAmount(
            line.HasNormalizedLots,
            line.LotCostSnapshotAmount,
            line.BaseCostAmount);
    }

    private static async Task<IReadOnlyDictionary<ExecutivePnLFormulaMonthKey, decimal>> LoadInventoryWeightedAverageUnitCostsAsync(
        IReportReadDbContext dbContext,
        IReadOnlyCollection<Guid> formulaIds,
        IReadOnlyCollection<DeliveryRevenueLine> revenueLines,
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        var formulaMaterials = await dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x => formulaIds.Contains(x.FormulaId) && x.IsActive && x.itemType == ItemType.Material)
            .Where(x => x.MaterialExternalIdSnapshot != null && x.MaterialExternalIdSnapshot != string.Empty)
            .Select(x => new { x.FormulaId, ProductCode = x.MaterialExternalIdSnapshot!, x.Quantity })
            .ToListAsync(cancellationToken);
        var monthStarts = revenueLines
            .Select(line => new DateTime(line.RevenueDate.Year, line.RevenueDate.Month, 1))
            .Distinct()
            .ToArray();
        var productCodes = formulaMaterials.Select(x => x.ProductCode).Distinct().ToArray();
        if (monthStarts.Length == 0 || productCodes.Length == 0)
        {
            return new Dictionary<ExecutivePnLFormulaMonthKey, decimal>();
        }

        var firstMonthStart = monthStarts.Min();
        var latestMonthEnd = monthStarts.Max().AddMonths(1);
        var openingLedgerEntries = await dbContext.InventoryStockLedgers
            .AsNoTracking()
            .Where(x => productCodes.Contains(x.ProductCode) && x.TxnDate < firstMonthStart)
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.AvgCostAfter.HasValue && x.AvgCostAfter.Value > 0m)
            .GroupBy(x => x.ProductCode)
            .Select(group => group
                .OrderByDescending(x => x.TxnDate)
                .ThenByDescending(x => x.LedgerId)
                .Select(x => new { x.ProductCode, x.TxnDate, x.LedgerId, AverageCost = x.AvgCostAfter!.Value })
                .First())
            .ToListAsync(cancellationToken);
        var periodLedgerEntries = await dbContext.InventoryStockLedgers
            .AsNoTracking()
            .Where(x => productCodes.Contains(x.ProductCode) && x.TxnDate >= firstMonthStart && x.TxnDate < latestMonthEnd)
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.AvgCostAfter.HasValue && x.AvgCostAfter.Value > 0m)
            .Select(x => new { x.ProductCode, x.TxnDate, x.LedgerId, AverageCost = x.AvgCostAfter!.Value })
            .ToListAsync(cancellationToken);
        var ledgerEntries = openingLedgerEntries.Concat(periodLedgerEntries).ToArray();

        var averageCostByProductMonth = monthStarts
            .SelectMany(monthStart => productCodes.Select(productCode => new { monthStart, productCode }))
            .Select(x => new
            {
                x.monthStart,
                x.productCode,
                Ledger = ledgerEntries
                    .Where(entry => entry.ProductCode == x.productCode && entry.TxnDate < x.monthStart.AddMonths(1))
                    .OrderByDescending(entry => entry.TxnDate)
                    .ThenByDescending(entry => entry.LedgerId)
                    .FirstOrDefault()
            })
            .Where(x => x.Ledger != null)
            .ToDictionary(x => (x.monthStart, x.productCode), x => x.Ledger!.AverageCost);

        return monthStarts.SelectMany(monthStart => formulaMaterials.GroupBy(x => x.FormulaId).Select(formula => new
            {
                Key = new ExecutivePnLFormulaMonthKey(formula.Key, monthStart),
                Materials = formula.ToArray()
            }))
            .Where(x => x.Materials.All(material => averageCostByProductMonth.ContainsKey((x.Key.MonthStart, material.ProductCode))))
            .ToDictionary(
                x => x.Key,
                x => x.Materials.Sum(material => material.Quantity * averageCostByProductMonth[(x.Key.MonthStart, material.ProductCode)]));
    }
}

internal sealed record ExecutivePnLDeliveryCostSources(
    IReadOnlyDictionary<Guid, decimal> FormulaUnitCostById,
    IReadOnlyDictionary<Guid, decimal> ActualWarehouseUnitCostByOrderDetail,
    IReadOnlyDictionary<ExecutivePnLFormulaMonthKey, decimal> InventoryWeightedAverageUnitCostByFormulaMonth);

internal readonly record struct ExecutivePnLFormulaMonthKey(Guid FormulaId, DateTime MonthStart)
{
    public static ExecutivePnLFormulaMonthKey From(DeliveryRevenueLine line) =>
        new(line.FormulaId, new DateTime(line.RevenueDate.Year, line.RevenueDate.Month, 1));
}
