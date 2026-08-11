using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Commons.Pricing.Helpers;

public static class FormulaRealtimeMaterialCostCalculator
{
    private const int MoneyScale = 6;

    public static FormulaRealtimeMaterialCostResult Calculate(
        IEnumerable<FormulaMaterialCostItem> items,
        IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> latestPriceByItem)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return new FormulaRealtimeMaterialCostResult(null, false, 0);
        }

        var total = 0m;
        var missingPriceCount = 0;

        foreach (var item in itemList)
        {
            var itemType = NormalizeItemType(item.ItemType);
            LatestItemPriceDto? latestPrice = null;
            var hasPrice = item.ItemId is { } itemId &&
                itemId != Guid.Empty &&
                latestPriceByItem.TryGetValue(new PriceItemKey(itemType, itemId), out latestPrice) &&
                latestPrice.PriceSource != LatestPriceSourceType.Unknown;

            if (!hasPrice)
            {
                missingPriceCount++;
                continue;
            }

            total += item.Quantity * latestPrice!.CurrentPrice;
        }

        return missingPriceCount == 0
            ? new FormulaRealtimeMaterialCostResult(
                decimal.Round(total, MoneyScale, MidpointRounding.AwayFromZero),
                true,
                0)
            : new FormulaRealtimeMaterialCostResult(null, false, missingPriceCount);
    }

    public static ItemType NormalizeItemType(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.MaterialFailure => ItemType.Material,
            ItemType.ProductFailure => ItemType.Product,
            _ => itemType
        };
    }
}
