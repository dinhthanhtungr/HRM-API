using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Projects realtime pricing sources according to the central pricing capability decision.
/// Approved persisted prices are handled separately from these system-calculated sources.
/// </summary>
internal static class ProductPricingSourceVisibility
{
    public static ProductPricingSourceOptionDto Apply(
        ProductPricingSourceOptionDto source,
        PricingAccessDecision access)
        => new()
        {
            PricingStatus = source.PricingStatus,
            FormulaPricingPolicyId = source.FormulaPricingPolicyId,
            FormulaPricingPolicyVersion = source.FormulaPricingPolicyVersion,
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            ExternalId = source.ExternalId,
            Name = source.Name,
            Status = source.Status,
            IsEligible = source.IsEligible,
            IsCustomerSelected = source.IsCustomerSelected,
            MaterialCostSnapshot = access.CanViewMaterialCost
                ? source.MaterialCostSnapshot
                : null,
            CurrentMaterialCost = access.CanViewMaterialCost
                ? source.CurrentMaterialCost
                : null,
            IsCurrentMaterialCostComplete = access.CanViewMaterialCost &&
                source.IsCurrentMaterialCostComplete,
            MissingMaterialPriceCount = access.CanViewMaterialCost
                ? source.MissingMaterialPriceCount
                : 0,
            ManufacturingCost = access.CanViewManufacturingCost
                ? source.ManufacturingCost
                : null,
            UsedDefaultManufacturingCost = access.CanViewManufacturingCost &&
                source.UsedDefaultManufacturingCost,
            StandardSellingPrice = access.CanViewSystemCalculatedPrice
                ? source.StandardSellingPrice
                : null,
            ProfitMarginRate = access.CanViewMargin
                ? source.ProfitMarginRate
                : null,
            PricingProfile = source.PricingProfile,
            PriceTierTemplates = access.CanViewSystemCalculatedPrice
                ? source.PriceTierTemplates
                : [],
            Pricing = access.CanViewMaterialCost &&
                      access.CanViewManufacturingCost &&
                      access.CanViewMargin &&
                      access.CanViewSystemCalculatedPrice
                ? source.Pricing
                : null,
            UpdatedDate = access.CanViewHistory ? source.UpdatedDate : null,
            Materials = access.CanViewMaterialCost ? source.Materials : []
        };
}
