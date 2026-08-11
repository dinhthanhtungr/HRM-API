using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.Dispatch.DeliveryOrders;

public static class DeliveryOrderCostVisibilityRules
{
    public static bool CanViewCost(ICurrentUser currentUser)
        => currentUser.IsAuthenticated &&
           currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaPriceViewers);
}
