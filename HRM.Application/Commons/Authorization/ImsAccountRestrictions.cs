using HRM.Application.Abstractions.Security;

namespace HRM.Application.Commons.Authorization;

public static class ImsAccountRestrictions
{
    public static bool AppliesTo(ICurrentUser user) =>
        user.IsAuthenticated && user.IsInRole(ApplicationRoles.InternalSystems.IMSUser);

    private static readonly HashSet<string> RestrictedPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        ApplicationPermissions.Pricing.ViewWorkbench,
        ApplicationPermissions.Pricing.ViewApprovedSellingPrice,
        ApplicationPermissions.Pricing.ViewSystemCalculatedPrice,
        ApplicationPermissions.Pricing.ViewMaterialCost,
        ApplicationPermissions.Pricing.ViewManufacturingCost,
        ApplicationPermissions.Pricing.ViewMargin,
        ApplicationPermissions.Pricing.ViewHistory,
        ApplicationPermissions.Pricing.Manage,
        ApplicationPermissions.Pricing.Approve,
        ApplicationPermissions.PLM.ViewFormulaPrices,
        ApplicationPermissions.PLM.ViewFormulaMaterials,
        ApplicationPermissions.PLM.ViewProductTechnicalInfo,
        ApplicationPermissions.PLM.ViewMaterialPriceReviewDetails,
        ApplicationPermissions.PLM.ConfirmCustomerPrice,
        ApplicationPermissions.PLM.ManageManufacturingFormulas,
        ApplicationPermissions.Dispatch.ViewDeliveryCost,
        ApplicationPermissions.Reports.ViewSalesProfit,
        ApplicationPermissions.Reports.ViewPlpu,
        ApplicationPermissions.Purchasing.ManagePurchaseOrders,
        ApplicationPermissions.Purchasing.ManageCatalog,
        ApplicationPermissions.InternalMail.ObservePricingGroup,
    };

    public static bool BlocksPermission(string permission) => RestrictedPermissions.Contains(permission);
}
