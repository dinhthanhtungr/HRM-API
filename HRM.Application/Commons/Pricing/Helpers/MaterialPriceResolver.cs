using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Commons.Pricing.Helpers
{
    public static class MaterialPriceResolver
    {
        /// <summary>
        /// Trả về giá mới nhất của nguyên vật liệu dựa trên dictionary đã cho. 
        /// Nếu không tìm thấy, trả về giá fallback hoặc 0 nếu fallback là null.
        /// </summary>
        /// <param name="priceDict"></param>
        /// <param name="materialId"></param>
        /// <param name="fallback"></param>
        /// <returns></returns>
        public static decimal ResolveLatestPrice(
            IReadOnlyDictionary<Guid, LatestMaterialPriceDto> priceDict,
            Guid? materialId,
            decimal? fallback = 0m)
        {
            if (materialId.HasValue &&
                materialId.Value != Guid.Empty &&
                priceDict.TryGetValue(materialId.Value, out var info))
            {
                return info.CurrentPrice;
            }

            return fallback ?? 0m;
        }

        /// <summary>
        /// Trả về ngày của giá mới nhất của nguyên vật liệu dựa trên dictionary đã cho.
        /// </summary>
        /// <param name="priceDict"></param>
        /// <param name="materialId"></param>
        /// <returns></returns>
        public static DateTime? ResolveLatestPriceDate(
            IReadOnlyDictionary<Guid, LatestMaterialPriceDto> priceDict,
            Guid? materialId)
        {
            if (materialId.HasValue &&
                materialId.Value != Guid.Empty &&
                priceDict.TryGetValue(materialId.Value, out var info))
            {
                return info.PriceDate;
            }

            return null;
        }

        /// <summary>
        /// Trả về nguồn của giá mới nhất của nguyên vật liệu dựa trên dictionary đã cho.
        /// </summary>
        /// <param name="priceDict"></param>
        /// <param name="materialId"></param>
        /// <returns></returns>
        public static MaterialPriceSource ResolveLatestPriceSource(
            IReadOnlyDictionary<Guid, LatestMaterialPriceDto> priceDict,
            Guid? materialId)
        {
            if (materialId.HasValue &&
                materialId.Value != Guid.Empty &&
                priceDict.TryGetValue(materialId.Value, out var info))
            {
                return info.PriceSource;
            }

            return MaterialPriceSource.Unknown;
        }

        /// <summary>
        /// Trả về giá mới nhất của một item (có thể là nguyên vật liệu hoặc sản phẩm) dựa trên dictionary đã cho.
        /// </summary>
        /// <param name="priceDict"></param>
        /// <param name="itemType"></param>
        /// <param name="itemId"></param>
        /// <param name="fallback"></param>
        /// <returns></returns>
        public static decimal ResolveLatestItemPrice(
            IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> priceDict,
            ItemType itemType,
            Guid? itemId,
            decimal fallback = 0m)
        {
            if (!itemId.HasValue || itemId.Value == Guid.Empty)
            {
                return fallback;
            }

            return priceDict.TryGetValue(new PriceItemKey(itemType, itemId.Value), out var info)
                ? info.CurrentPrice
                : fallback;
        }

        /// <summary>
        /// Trả về ngày của giá mới nhất của một item (có thể là nguyên vật liệu hoặc sản phẩm) dựa trên dictionary đã cho.
        /// </summary>
        /// <param name="priceDict"></param>
        /// <param name="itemType"></param>
        /// <param name="itemId"></param>
        /// <returns></returns>
        public static DateTime? ResolveLatestItemPriceDate(
            IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> priceDict,
            ItemType itemType,
            Guid? itemId)
        {
            if (!itemId.HasValue || itemId.Value == Guid.Empty)
            {
                return null;
            }

            return priceDict.TryGetValue(new PriceItemKey(itemType, itemId.Value), out var info)
                ? info.PriceDate
                : null;
        }

        public static LatestPriceSourceType ResolveLatestItemPriceSource(
            IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> priceDict,
            ItemType itemType,
            Guid? itemId)
        {
            if (!itemId.HasValue || itemId.Value == Guid.Empty)
            {
                return LatestPriceSourceType.Unknown;
            }

            return priceDict.TryGetValue(new PriceItemKey(itemType, itemId.Value), out var info)
                ? info.PriceSource
                : LatestPriceSourceType.Unknown;
        }
    }
}
