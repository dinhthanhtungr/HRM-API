using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Pricing.Authorization;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingAccessRules
{
    public static bool CanViewManagementData(ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated ||
            !currentUser.IsInAnyRole(ApplicationRoleSets.Pricing.ExecutiveReaders))
            return false;

        var access = new PricingVisibilityService(new CurrentUserPermissionService(currentUser)).GetAccess();
        return access.CanViewWorkbench && access.CanViewApprovedSellingPrice &&
            access.CanViewSystemCalculatedPrice && access.CanViewMaterialCost &&
            access.CanViewManufacturingCost && access.CanViewMargin && access.CanViewHistory;
    }

    public static bool CanViewWorkbench(ICurrentUser currentUser)
        => CanManage(currentUser) ||
           currentUser.IsInRole(ApplicationRoles.Sales.SaleUser);

    public static bool CanManage(ICurrentUser currentUser)
        => currentUser.IsInRole(ApplicationRoles.President) ||
           currentUser.IsInRole(ApplicationRoles.Developer);
}
