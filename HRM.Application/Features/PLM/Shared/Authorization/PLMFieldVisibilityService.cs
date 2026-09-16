using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.Shared.Authorization;

internal sealed class PLMFieldVisibilityService : IPLMFieldVisibilityService
{
    private readonly ICurrentUserPermissionService _permissionService;

    public PLMFieldVisibilityService(ICurrentUserPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public bool CanViewFormulaPrices()
    {
        return _permissionService.HasPermission(ApplicationPermissions.PLM.ViewFormulaPrices);
    }

    public bool CanViewFormulaMaterials()
    {
        return _permissionService.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials);
    }

    public bool CanViewProductTechnicalInfo()
    {
        return _permissionService.HasPermission(ApplicationPermissions.PLM.ViewProductTechnicalInfo);
    }

    public bool CanViewMaterialPriceReviewDetails()
    {
        return _permissionService.HasPermission(ApplicationPermissions.PLM.ViewMaterialPriceReviewDetails);
    }
}
