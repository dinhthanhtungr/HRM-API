using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.Warehouse.Services;

/// <summary>Centralizes read permission and valid company scope for voucher history.</summary>
public static class WarehouseVoucherAccessRules
{
    public static bool CanViewSupplier(ICurrentUser user, ICurrentUserPermissionService permissions) =>
        user.IsAuthenticated && user.CompanyId is { } company && company != Guid.Empty &&
        !ImsAccountRestrictions.AppliesTo(user) &&
        user.IsInAnyRole(ApplicationRoleSets.Warehouse.VoucherSupplierViewers) &&
        permissions.HasPermission(ApplicationPermissions.Warehouse.ViewVoucherSuppliers);

    public static bool CanRead(ICurrentUser currentUser) =>
        currentUser.IsAuthenticated &&
        currentUser.CompanyId is { } companyId && companyId != Guid.Empty &&
        (ImsAccountRestrictions.AppliesTo(currentUser) || CanReadAll(currentUser) ||
         currentUser.IsInRole(ApplicationRoles.Sales.SaleUser) ||
         currentUser.IsInRole(ApplicationRoles.Sales.SaleAdmin));

    public static bool CanReadAll(ICurrentUser currentUser) =>
        currentUser.IsAuthenticated &&
        currentUser.CompanyId is { } companyId && companyId != Guid.Empty &&
        (ApplicationRoleSets.Modules.Warehouse.Any(currentUser.IsInRole) ||
         ApplicationRoleSets.Modules.Lab.Any(currentUser.IsInRole) ||
         currentUser.IsInAnyRole(ApplicationRoleSets.Warehouse.VoucherSupplierViewers) ||
         currentUser.IsInAnyRole(ApplicationRoleSets.CRM.ExecutiveReaders));
}
