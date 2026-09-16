using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.Pricing.Authorization;

internal sealed class PricingVisibilityService : IPricingVisibilityService
{
    private readonly ICurrentUserPermissionService _permissionService;

    public PricingVisibilityService(ICurrentUserPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public PricingAccessDecision GetAccess()
        => new(
            CanViewWorkbench: Has(ApplicationPermissions.Pricing.ViewWorkbench),
            CanViewApprovedSellingPrice: Has(ApplicationPermissions.Pricing.ViewApprovedSellingPrice),
            CanViewSystemCalculatedPrice: Has(ApplicationPermissions.Pricing.ViewSystemCalculatedPrice),
            CanViewMaterialCost: Has(ApplicationPermissions.Pricing.ViewMaterialCost),
            CanViewManufacturingCost: Has(ApplicationPermissions.Pricing.ViewManufacturingCost),
            CanViewMargin: Has(ApplicationPermissions.Pricing.ViewMargin),
            CanViewHistory: Has(ApplicationPermissions.Pricing.ViewHistory),
            CanManage: Has(ApplicationPermissions.Pricing.Manage),
            CanApprove: Has(ApplicationPermissions.Pricing.Approve));

    private bool Has(string permission) => _permissionService.HasPermission(permission);
}
