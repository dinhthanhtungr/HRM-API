using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class ProductPricingAccessRules
{
    public static bool CanViewWorkbench(ICurrentUser currentUser)
        => CanManage(currentUser) ||
           currentUser.IsInRole(ApplicationRoles.Sales.SaleUser);

    public static bool CanManage(ICurrentUser currentUser)
        => currentUser.IsInRole(ApplicationRoles.President) ||
           currentUser.IsInRole(ApplicationRoles.Developer);
}
