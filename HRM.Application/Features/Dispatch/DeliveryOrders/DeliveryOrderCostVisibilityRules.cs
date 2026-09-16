using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.Dispatch.DeliveryOrders;

public static class DeliveryOrderCostVisibilityRules
{
    public static bool CanViewCost(ICurrentUserPermissionService permissionService)
        => permissionService.HasPermission(ApplicationPermissions.Dispatch.ViewDeliveryCost);
}
