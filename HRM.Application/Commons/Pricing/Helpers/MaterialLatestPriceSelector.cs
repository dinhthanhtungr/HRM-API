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
                return Compare(purchaseOrderPrice!, supplierPrice!) >= 0
                    ? purchaseOrderPrice!
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

        private static int Compare(MaterialPriceCandidate left, MaterialPriceCandidate right)
        {
            var dateComparison = CompareNullable(left.PriceDate, right.PriceDate);
            if (dateComparison != 0)
            {
                return dateComparison;
            }

            var sourceComparison = GetSourcePriority(left.PriceSource)
                .CompareTo(GetSourcePriority(right.PriceSource));
            if (sourceComparison != 0)
            {
                return sourceComparison;
            }

            if (left.PriceSource == MaterialPriceSource.MaterialSupplier)
            {
                var preferredComparison = left.IsPreferred.CompareTo(right.IsPreferred);
                if (preferredComparison != 0)
                {
                    return preferredComparison;
                }
            }

            var idComparison = left.CandidateId.CompareTo(right.CandidateId);
            if (idComparison != 0)
            {
                return idComparison;
            }

            return left.CurrentPrice.CompareTo(right.CurrentPrice);
        }

        private static int CompareNullable(DateTime? left, DateTime? right)
            => left.HasValue
                ? right.HasValue ? left.Value.CompareTo(right.Value) : 1
                : right.HasValue ? -1 : 0;

        private static int GetSourcePriority(MaterialPriceSource source)
            => source switch
            {
                MaterialPriceSource.PurchaseOrder => 2,
                MaterialPriceSource.MaterialSupplier => 1,
                _ => 0
            };
    }
}
