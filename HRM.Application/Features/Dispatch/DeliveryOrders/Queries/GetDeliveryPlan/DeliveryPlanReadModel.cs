using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;

internal sealed class DeliveryPlanReadModel(IDispatchReadDbContext db)
{
    internal async Task<Dictionary<Guid, List<DeliveryPlanLineDto>>> LoadAsync(Guid[] orderIds, Guid companyId, CancellationToken ct)
    {
        var deliveredDetails = DeliveryPlanSourceQuery.CountedAsDelivered(db.DeliveryOrderDetails.AsNoTracking(), companyId);
        var lines = await db.MerchandiseOrderDetails.AsNoTracking()
            .Where(x => x.IsActive && orderIds.Contains(x.MerchandiseOrderId) && x.MerchandiseOrder.IsActive &&
                x.MerchandiseOrder.CompanyId == companyId)
            .OrderBy(x => x.ProductExternalIdSnapshot).ThenBy(x => x.MerchandiseOrderDetailId)
            .Select(x => new { x.MerchandiseOrderId, x.MerchandiseOrderDetailId, x.ProductId,
                x.ProductExternalIdSnapshot, x.ProductNameSnapshot, x.ExpectedQuantity, x.Comment,
                DeliveredQuantity = deliveredDetails.Where(d => d.MerchandiseOrderDetailId == x.MerchandiseOrderDetailId)
                    .Sum(d => (decimal?)d.Quantity) ?? 0m }).ToListAsync(ct);
        var ids = lines.Select(x => x.MerchandiseOrderDetailId).ToArray();
        var productCodes = lines.Select(x => x.ProductExternalIdSnapshot)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        var stockByProduct = await CurrentProductStockTotals(db.WarehouseShelfStocks.AsNoTracking(), companyId)
            .Where(x => productCodes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, ct);
        var production = await DeliveryPlanSourceQuery.ActiveLinks(db.MfgOrderPOs.AsNoTracking(), companyId)
            .Where(x => ids.Contains(x.MerchandiseOrderDetailId))
            .OrderBy(x => x.ProductionOrder.ExpectedDate).ThenBy(x => x.MfgProductionOrderId)
            .Select(x => new
            {
                x.MerchandiseOrderDetailId,
                Order = new DeliveryPlanProductionOrderDto
                {
                    Id = x.MfgProductionOrderId, ExternalId = x.ProductionOrder.ExternalId, Status = x.ProductionOrder.Status,
                    PromisedDeliveryDate = x.ProductionOrder.ExpectedDate, ProductionQuantity = x.ProductionOrder.TotalQuantity,
                    Lots = x.ProductionOrder.ProductionSelectVersions.Where(v => v.ValidTo == null && v.ManufacturingFormula != null &&
                        v.ManufacturingFormula.IsActive && v.ManufacturingFormula.CompanyId == companyId)
                        .Select(v => v.ManufacturingFormula!.ExternalId).Distinct().OrderBy(code => code).ToList()
                }
            }).ToListAsync(ct);
        var deliveries = await db.DeliveryOrderDetails.AsNoTracking().CountedForAllocation(companyId)
            .Where(x => ids.Contains(x.MerchandiseOrderDetailId!.Value))
            .OrderBy(x => x.DeliveryOrder.CreatedDate).ThenBy(x => x.Id)
            .Select(x => new
            {
                DetailId = x.MerchandiseOrderDetailId!.Value,
                Order = new DeliveryPlanOrderDto
                {
                    Id = x.DeliveryOrderId, ExternalId = x.DeliveryOrder.ExternalId, Status = x.DeliveryOrder.Status,
                    HasPrinted = x.DeliveryOrder.HasPrinted, Quantity = x.Quantity,
                    Deliverers = x.DeliveryOrder.Deliverers.Where(d => d.DelivererInfor.IsActive)
                        .OrderBy(d => d.DelivererInfor.Name).Select(d => new DeliveryPlanDelivererDto(d.DelivererInforId, d.DelivererInfor.Name)).ToList()
                }
            }).ToListAsync(ct);
        var productionByLine = production.GroupBy(x => x.MerchandiseOrderDetailId).ToDictionary(g => g.Key, g => g.Select(x => x.Order).ToArray());
        var deliveriesByLine = deliveries.GroupBy(x => x.DetailId).ToDictionary(g => g.Key,
            g => g.GroupBy(x => x.Order.Id).Select(group => new DeliveryPlanOrderDto
            {
                Id = group.Key, ExternalId = group.First().Order.ExternalId, Status = group.First().Order.Status,
                HasPrinted = group.First().Order.HasPrinted, Quantity = group.Sum(x => x.Order.Quantity), Deliverers = group.First().Order.Deliverers
            }).ToArray());
        return lines.GroupBy(x => x.MerchandiseOrderId).ToDictionary(g => g.Key, g => g.Select(line =>
        {
            var orders = deliveriesByLine.GetValueOrDefault(line.MerchandiseOrderDetailId) ?? [];
            var delivered = line.DeliveredQuantity;
            var allocated = orders.Sum(x => x.Quantity);
            var remaining = Math.Max(0m, line.ExpectedQuantity - delivered);
            var stock = line.ProductExternalIdSnapshot is { } code ? stockByProduct.GetValueOrDefault(code) : null;
            return new DeliveryPlanLineDto
            {
                MerchandiseOrderDetailId = line.MerchandiseOrderDetailId, ProductId = line.ProductId,
                ProductCode = line.ProductExternalIdSnapshot, ProductName = line.ProductNameSnapshot, Note = line.Comment,
                OrderedQuantity = line.ExpectedQuantity, DeliveredQuantity = delivered, RemainingQuantity = remaining,
                ImportedQuantity = (stock?.FinishedGoodQuantity ?? 0m) + (stock?.DefectiveFinishedGoodQuantity ?? 0m),
                StockBreakdown = new DeliveryPlanStockBreakdownDto
                {
                    FinishedGoodQuantity = stock?.FinishedGoodQuantity ?? 0m,
                    DefectiveFinishedGoodQuantity = stock?.DefectiveFinishedGoodQuantity ?? 0m
                },
                AllocatedQuantity = allocated, UnallocatedQuantity = Math.Max(0m, line.ExpectedQuantity - allocated),
                Progress = Progress(delivered, remaining), ProductionOrders = productionByLine.GetValueOrDefault(line.MerchandiseOrderDetailId) ?? [],
                DeliveryOrders = orders
            };
        }).ToList());
    }

    internal static DeliveryPlanProgress Progress(decimal delivered, decimal remaining)
        => remaining <= 0m ? DeliveryPlanProgress.Delivered
            : delivered > 0m ? DeliveryPlanProgress.PartiallyDelivered : DeliveryPlanProgress.NotDelivered;

    internal static IQueryable<WarehouseShelfStock> CurrentProductStock(IQueryable<WarehouseShelfStock> stocks, Guid companyId)
        => stocks.Where(x => x.CompanyId == companyId && x.WarehouseShelves != null && x.WarehouseShelves.IsActive &&
            (x.StockType == StockType.FinishedGood || x.StockType == StockType.DefectiveFinishedGood));

    internal static IQueryable<DeliveryPlanProductStock> CurrentProductStockTotals(IQueryable<WarehouseShelfStock> stocks, Guid companyId)
        => CurrentProductStock(stocks, companyId).GroupBy(x => x.Code)
            .Select(g => new DeliveryPlanProductStock
            {
                Code = g.Key,
                FinishedGoodQuantity = g.Sum(x => x.StockType == StockType.FinishedGood ? x.QtyKg : 0m),
                DefectiveFinishedGoodQuantity = g.Sum(x => x.StockType == StockType.DefectiveFinishedGood ? x.QtyKg : 0m)
            });
}

internal sealed class DeliveryPlanProductStock
{
    public string Code { get; init; } = string.Empty;
    public decimal FinishedGoodQuantity { get; init; }
    public decimal DefectiveFinishedGoodQuantity { get; init; }
}
