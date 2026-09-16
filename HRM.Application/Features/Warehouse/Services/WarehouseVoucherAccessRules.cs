using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.Warehouse.Services;

/// <summary>Centralizes read permission and valid company scope for voucher history.</summary>
public static class WarehouseVoucherAccessRules
{
    public static bool CanRead(ICurrentUser currentUser) =>
        currentUser.IsAuthenticated &&
        currentUser.CompanyId is { } companyId && companyId != Guid.Empty &&
        (ApplicationRoleSets.Modules.Warehouse.Any(currentUser.IsInRole) ||
         currentUser.IsInRole(ApplicationRoles.Sales.SaleUser) ||
         currentUser.IsInRole(ApplicationRoles.Sales.SaleAdmin));

    public static bool CanReadAll(ICurrentUser currentUser) =>
        ApplicationRoleSets.Modules.Warehouse.Any(currentUser.IsInRole);
}
