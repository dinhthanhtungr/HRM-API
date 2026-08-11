namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetCustomerProductStock;

internal static class CustomerProductStockCalculator
{
    public static CustomerProductStockCalculation Calculate(
        IReadOnlyCollection<CustomerProductFormulaSource> formulaSources,
        IReadOnlyDictionary<Guid, int> formulaCustomerCounts,
        IReadOnlyCollection<CustomerProductShelfStock> productStock,
        IReadOnlyCollection<CustomerProductReservation> productReservations)
    {
        var sourceByLot = formulaSources
            .GroupBy(x => Normalize(x.LotNo))
            .ToDictionary(x => x.Key, x => x.ToList());
        var stockLotKeyMap = productStock
            .Where(x => !string.IsNullOrWhiteSpace(x.LotKey))
            .GroupBy(x => Normalize(x.LotKey))
            .ToDictionary(
                x => x.Key,
                x => Normalize(EffectiveLot(x.OrderBy(y => y.ShelfStockId).First())));
        var reservedByLot = productReservations
            .Where(x => x.RemainingKg > 0m && !string.IsNullOrWhiteSpace(x.LotKey))
            .GroupBy(x => stockLotKeyMap.GetValueOrDefault(Normalize(x.LotKey), Normalize(x.LotKey)))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.RemainingKg));

        var productOnHand = productStock.Sum(x => x.OnHandKg);
        var productReserved = productReservations.Where(x => x.RemainingKg > 0m).Sum(x => x.RemainingKg);
        var productAvailable = Math.Max(0m, productOnHand - productReserved);
        var lots = new List<CustomerProductStockLotCalculation>();

        foreach (var stockGroup in productStock
                     .Where(x => sourceByLot.ContainsKey(Normalize(EffectiveLot(x))))
                     .GroupBy(x => Normalize(EffectiveLot(x)))
                     .OrderBy(x => x.Key))
        {
            var sources = sourceByLot[stockGroup.Key];
            var formulaId = sources[0].ManufacturingFormulaId;
            var isAmbiguous = formulaCustomerCounts.GetValueOrDefault(formulaId) > 1;
            var onHand = stockGroup.Sum(x => x.OnHandKg);
            var reserved = reservedByLot.GetValueOrDefault(stockGroup.Key);
            var available = Math.Max(0m, onHand - reserved);
            var shelves = stockGroup
                .GroupBy(x => x.ShelfCode)
                .OrderBy(x => x.Key)
                .Select(x => new CustomerProductShelfCalculation(x.Key, x.Sum(y => y.OnHandKg)))
                .ToList();
            var mfgOrders = sources
                .GroupBy(x => x.MfgProductionOrderId)
                .Select(x => x.First())
                .OrderByDescending(x => x.MfgCreatedDate)
                .ThenByDescending(x => x.MfgProductionOrderId)
                .Select(x => new CustomerProductMfgCalculation(
                    x.MfgProductionOrderId,
                    x.MfgExternalId))
                .ToList();

            lots.Add(new CustomerProductStockLotCalculation(
                formulaId,
                sources[0].LotNo,
                onHand,
                reserved,
                available,
                isAmbiguous,
                mfgOrders,
                shelves));
        }

        var attributableLots = lots.Where(x => !x.IsAttributionAmbiguous).ToList();
        var attributableAvailable = attributableLots.Sum(x => x.AvailableKg);
        return new CustomerProductStockCalculation(
            attributableLots.Sum(x => x.OnHandKg),
            attributableLots.Sum(x => x.ReservedOpenKg),
            Math.Min(attributableAvailable, productAvailable),
            productAvailable,
            lots.Where(x => x.IsAttributionAmbiguous).Sum(x => x.OnHandKg),
            lots);
    }

    private static string EffectiveLot(CustomerProductShelfStock stock)
        => !string.IsNullOrWhiteSpace(stock.LotNo) ? stock.LotNo! : stock.LotKey ?? string.Empty;

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}

internal sealed record CustomerProductFormulaSource(
    Guid ManufacturingFormulaId,
    string LotNo,
    Guid MfgProductionOrderId,
    string MfgExternalId,
    DateTime MfgCreatedDate);

internal sealed record CustomerProductShelfStock(
    int ShelfStockId,
    string ShelfCode,
    string? LotNo,
    string? LotKey,
    decimal OnHandKg);

internal sealed record CustomerProductReservation(string? LotKey, decimal RemainingKg);

internal sealed record CustomerProductStockCalculation(
    decimal TotalOnHandKg,
    decimal ReservedOpenKg,
    decimal AvailableKg,
    decimal ProductAvailableKg,
    decimal AmbiguousOnHandKg,
    IReadOnlyList<CustomerProductStockLotCalculation> Lots);

internal sealed record CustomerProductStockLotCalculation(
    Guid ManufacturingFormulaId,
    string LotNo,
    decimal OnHandKg,
    decimal ReservedOpenKg,
    decimal AvailableKg,
    bool IsAttributionAmbiguous,
    IReadOnlyList<CustomerProductMfgCalculation> SourceMfgOrders,
    IReadOnlyList<CustomerProductShelfCalculation> Shelves);

internal sealed record CustomerProductMfgCalculation(Guid MfgProductionOrderId, string ExternalId);

internal sealed record CustomerProductShelfCalculation(string ShelfCode, decimal OnHandKg);
