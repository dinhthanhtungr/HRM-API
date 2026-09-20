using HRM.Application.Commons.Pricing.Helpers;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.Executive.ProductPricingReview.Services;

internal sealed record ProductPricingReviewItemIdentity(
    ItemType ItemType,
    Guid? MaterialId,
    Guid? ProductId)
{
    public static ProductPricingReviewItemIdentity Resolve(ItemType itemType, Guid? itemId)
    {
        var normalizedType = FormulaRealtimeMaterialCostCalculator.NormalizeItemType(itemType);
        return normalizedType == ItemType.Material
            ? new ProductPricingReviewItemIdentity(normalizedType, itemId, null)
            : new ProductPricingReviewItemIdentity(normalizedType, null, itemId);
    }
}
