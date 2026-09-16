namespace HRM.Application.Features.Pricing.Authorization;

/// <summary>
/// Intersects data capabilities with the access boundary of a concrete pricing surface.
/// This prevents a broad data capability from silently granting access to a new feature.
/// </summary>
public static class PricingAccessScopes
{
    public static PricingAccessDecision ForQuotationPricingOptions(
        PricingAccessDecision access)
        => access with
        {
            CanViewApprovedSellingPrice = access.CanViewWorkbench &&
                access.CanViewApprovedSellingPrice,
            CanViewSystemCalculatedPrice = access.CanViewWorkbench &&
                access.CanViewSystemCalculatedPrice,
            CanViewMaterialCost = access.CanManage && access.CanViewMaterialCost,
            CanViewManufacturingCost = access.CanManage && access.CanViewManufacturingCost,
            CanViewMargin = access.CanManage && access.CanViewMargin,
            CanViewHistory = access.CanManage && access.CanViewHistory
        };
}
