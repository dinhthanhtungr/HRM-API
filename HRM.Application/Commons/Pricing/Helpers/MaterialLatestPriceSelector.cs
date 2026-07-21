using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;

namespace HRM.Application.Commons.Pricing.Helpers
{
    public static class MaterialLatestPriceSelector
    {
        public static Dictionary<Guid, LatestMaterialPriceDto> SelectMany(
            IReadOnlyCollection<Guid> materialIds,
            IReadOnlyDictionary<Guid, MaterialPriceCandidate> purchaseOrderPrices,
            IReadOnlyDictionary<Guid, MaterialPriceCandidate> supplierPrices)
        {
            var result = new Dictionary<Guid, LatestMaterialPriceDto>(materialIds.Count);

            foreach (var materialId in materialIds)
            {
                purchaseOrderPrices.TryGetValue(materialId, out var purchaseOrderPrice);
                supplierPrices.TryGetValue(materialId, out var supplierPrice);

                result[materialId] = ToLatestMaterialPrice(
                    materialId,
                    Select(materialId, purchaseOrderPrice, supplierPrice));
            }

            return result;
        }

        public static MaterialPriceCandidate Select(
            Guid materialId,
            MaterialPriceCandidate? purchaseOrderPrice,
            MaterialPriceCandidate? supplierPrice)
        {
            var hasValidPurchaseOrderPrice = purchaseOrderPrice is not null && purchaseOrderPrice.HasValidPrice;
            var hasValidSupplierPrice = supplierPrice is not null && supplierPrice.HasValidPrice;

            if (hasValidPurchaseOrderPrice && hasValidSupplierPrice)
            {
                return IsNewerOrSameDate(purchaseOrderPrice!.PriceDate, supplierPrice!.PriceDate)
                    ? purchaseOrderPrice
                    : supplierPrice!;
            }

            if (hasValidPurchaseOrderPrice)
            {
                return purchaseOrderPrice!;
            }

            if (hasValidSupplierPrice)
            {
                return supplierPrice!;
            }

            return CreateUnknown(materialId);
        }

        private static LatestMaterialPriceDto ToLatestMaterialPrice(
            Guid materialId,
            MaterialPriceCandidate selectedPrice)
        {
            return new LatestMaterialPriceDto
            {
                MaterialId = materialId,
                CurrentPrice = selectedPrice.CurrentPrice,
                PriceDate = selectedPrice.PriceDate,
                PriceSource = selectedPrice.PriceSource
            };
        }

        private static MaterialPriceCandidate CreateUnknown(Guid materialId)
        {
            return new MaterialPriceCandidate
            {
                MaterialId = materialId,
                CurrentPrice = 0m,
                PriceDate = null,
                PriceSource = MaterialPriceSource.Unknown
            };
        }

        private static bool IsNewerOrSameDate(DateTime? sourceDate, DateTime? compareDate)
        {
            if (!sourceDate.HasValue)
            {
                return !compareDate.HasValue;
            }

            if (!compareDate.HasValue)
            {
                return true;
            }

            return sourceDate.Value >= compareDate.Value;
        }
    }
}
