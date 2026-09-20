using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Application.Features.PLM.Materials.Rules;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Materials;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingMaterialAvailabilityRules
{
    public static MaterialPurchaseAvailabilityInfoDto? Resolve(
        ItemType itemType,
        Guid? itemId,
        MaterialPurchaseStatus? status,
        string? reason,
        DateTime? effectiveFrom,
        DateTime? expectedAvailableDate)
    {
        if (itemType is not (ItemType.Material or ItemType.MaterialFailure) ||
            !itemId.HasValue ||
            itemId.Value == Guid.Empty)
        {
            return null;
        }

        return MaterialPurchaseAvailabilityRules.Resolve(
            status,
            reason,
            effectiveFrom,
            expectedAvailableDate);
    }
}
