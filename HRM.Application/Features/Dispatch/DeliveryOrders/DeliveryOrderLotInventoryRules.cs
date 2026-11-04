using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Dispatch.DeliveryOrders;

internal static class DeliveryOrderLotInventoryRules
{
    public static string Key(string productCode, string lotNo, StockType stockType = StockType.FinishedGood)
        => $"{Normalize(productCode)}|{stockType}|{Normalize(lotNo)}";

    public static string ProductKey(string productCode, StockType stockType) => $"{Normalize(productCode)}|{stockType}";

    public static string Normalize(string value)
        => value.Trim().ToUpperInvariant();

    public static decimal CalculateTotalCost(decimal quantity, decimal unitCost)
        => decimal.Round(quantity * unitCost, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Tổng lot không được vượt tồn thực tế product/type; không trừ reserve trong luồng giao hàng.
    /// Chỉ truyền các snapshot thuộc cùng một product.
    /// </summary>
    public static decimal CalculateAvailableQuantity(IEnumerable<DeliveryOrderLotInventorySnapshot> lots)
    {
        var rows = lots.ToArray();
        return rows.GroupBy(x => x.StockType).Sum(group => Math.Max(0m, Math.Min(
            group.Sum(x => Math.Max(0m, x.AvailableQuantity)), group.Min(x => x.ProductAvailableQuantity))));
    }

    public static string? Validate(
        IReadOnlyCollection<DeliveryOrderRequestedLot> requestedLots,
        IReadOnlyDictionary<string, DeliveryOrderLotInventorySnapshot> inventory)
    {
        foreach (var requested in requestedLots
                     .GroupBy(x => Key(x.ProductCode, x.LotNo, x.StockType))
                     .Select(g => new { Key = g.Key, Quantity = g.Sum(x => x.Quantity), First = g.First() }))
        {
            if (!inventory.TryGetValue(requested.Key, out var stock))
            {
                return $"Lot {requested.First.LotNo} không hợp lệ cho product {requested.First.ProductCode}.";
            }

            if (stock.ProductId != requested.First.ProductId)
            {
                return $"Lot {requested.First.LotNo} không thuộc đúng product được chọn.";
            }

            if (requested.Quantity > stock.AvailableQuantity)
            {
                return $"Lot {requested.First.LotNo} không đủ tồn. Khả dụng: {stock.AvailableQuantity}.";
            }
        }

        foreach (var product in requestedLots.GroupBy(x => ProductKey(x.ProductCode, x.StockType)))
        {
            var requestedQuantity = product.Sum(x => x.Quantity);
            var productAvailable = inventory.Values
                .Where(x => ProductKey(x.ProductCode, x.StockType) == product.Key)
                .Select(x => x.ProductAvailableQuantity)
                .DefaultIfEmpty(0m)
                .First();

            if (requestedQuantity > productAvailable)
            {
                return $"Product {product.First().ProductCode} không đủ tồn khả dụng. Khả dụng: {productAvailable}.";
            }
        }

        return null;
    }
}

internal sealed record DeliveryOrderRequestedLot(
    Guid ProductId,
    string ProductCode,
    string LotNo,
    decimal Quantity,
    StockType StockType = StockType.FinishedGood);

internal sealed record DeliveryOrderLotInventorySnapshot(
    Guid ProductId,
    string ProductCode,
    string LotNo,
    decimal OnHandQuantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal ProductAvailableQuantity,
    decimal UnitCostSnapshot,
    StockType StockType = StockType.FinishedGood);
