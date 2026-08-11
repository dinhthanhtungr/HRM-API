using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders;

internal sealed class DeliveryOrderLotInventoryService
{
    private readonly IDispatchReadDbContext _dbContext;

    public DeliveryOrderLotInventoryService(IDispatchReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<string, DeliveryOrderLotInventorySnapshot>> LoadAsync(
        Guid companyId,
        IReadOnlyCollection<(Guid ProductId, string ProductCode)> products,
        bool includeCost,
        CancellationToken cancellationToken)
    {
        var productMap = products
            .Where(x => x.ProductId != Guid.Empty && !string.IsNullOrWhiteSpace(x.ProductCode))
            .GroupBy(x => DeliveryOrderLotInventoryRules.Normalize(x.ProductCode))
            .ToDictionary(x => x.Key, x => x.First());
        var productCodes = productMap.Values.Select(x => x.ProductCode).Distinct().ToArray();

        var stockRows = await _dbContext.WarehouseShelfStocks
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                productCodes.Contains(x.Code!) &&
                x.StockType == StockType.FinishedGood &&
                x.WarehouseShelves != null &&
                x.WarehouseShelves.IsActive &&
                ((x.LotNo != null && x.LotNo != string.Empty) ||
                 (x.LotKey != null && x.LotKey != string.Empty)))
            .Select(x => new
            {
                ProductCode = x.Code!,
                LotNo = x.LotNo != null && x.LotNo != string.Empty ? x.LotNo : x.LotKey!,
                x.LotKey,
                x.QtyKg
            })
            .ToListAsync(cancellationToken);

        var reserveRows = await _dbContext.WarehouseTempStocks
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                productCodes.Contains(x.Code) &&
                x.ReserveStatus == ReserveStatus.Open.ToString())
            .Select(x => new
            {
                x.Code,
                x.LotKey,
                Remaining = (x.QtyRequest ?? 0m) - (x.QtyUsed ?? 0m)
            })
            .Where(x => x.Remaining > 0m)
            .ToListAsync(cancellationToken);

        var costMap = new Dictionary<string, decimal>();
        if (includeCost)
        {
            var lotCodes = stockRows.Select(x => x.LotNo).Distinct().ToArray();
            var formulaRows = await _dbContext.ManufacturingFormulas
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.IsActive && lotCodes.Contains(x.ExternalId))
                .Select(x => new
                {
                    x.ExternalId,
                    x.UpdatedDate,
                    x.ManufacturingFormulaId,
                    UnitCost = x.ManufacturingFormulaMaterials
                        .Where(m => m.IsActive && m.itemType == ItemType.Material)
                        .Sum(m => (decimal?)m.TotalPrice) ?? 0m
                })
                .ToListAsync(cancellationToken);

            costMap = formulaRows
                .GroupBy(x => DeliveryOrderLotInventoryRules.Normalize(x.ExternalId))
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderByDescending(y => y.UpdatedDate)
                        .ThenByDescending(y => y.ManufacturingFormulaId)
                        .First().UnitCost);
        }

        var onHandByLot = stockRows
            .GroupBy(x => DeliveryOrderLotInventoryRules.Key(x.ProductCode, x.LotNo))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.QtyKg));
        var stockLotKeyMap = stockRows
            .Where(x => !string.IsNullOrWhiteSpace(x.LotKey))
            .GroupBy(x => DeliveryOrderLotInventoryRules.Key(x.ProductCode, x.LotKey!))
            .ToDictionary(
                x => x.Key,
                x => DeliveryOrderLotInventoryRules.Key(x.First().ProductCode, x.First().LotNo));
        var reservedByLot = reserveRows
            .Where(x => !string.IsNullOrWhiteSpace(x.LotKey))
            .GroupBy(x =>
            {
                var reserveKey = DeliveryOrderLotInventoryRules.Key(x.Code, x.LotKey!);
                return stockLotKeyMap.GetValueOrDefault(reserveKey, reserveKey);
            })
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Remaining));
        var productAvailable = stockRows
            .GroupBy(x => DeliveryOrderLotInventoryRules.Normalize(x.ProductCode))
            .ToDictionary(
                x => x.Key,
                x => x.Sum(y => y.QtyKg) - reserveRows
                    .Where(r => DeliveryOrderLotInventoryRules.Normalize(r.Code) == x.Key)
                    .Sum(r => r.Remaining));

        return stockRows
            .GroupBy(x => DeliveryOrderLotInventoryRules.Key(x.ProductCode, x.LotNo))
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var row = x.First();
                    var normalizedProduct = DeliveryOrderLotInventoryRules.Normalize(row.ProductCode);
                    var normalizedLot = DeliveryOrderLotInventoryRules.Normalize(row.LotNo);
                    var onHand = onHandByLot[x.Key];
                    var reserved = reservedByLot.GetValueOrDefault(x.Key);
                    return new DeliveryOrderLotInventorySnapshot(
                        productMap[normalizedProduct].ProductId,
                        row.ProductCode,
                        row.LotNo,
                        onHand,
                        reserved,
                        Math.Max(0m, onHand - reserved),
                        Math.Max(0m, productAvailable.GetValueOrDefault(normalizedProduct)),
                        costMap.GetValueOrDefault(normalizedLot));
                });
    }
}
