using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

internal static class PurchaseOrderAccess
{
    public static Guid RequireCompanyId(ICurrentUser currentUser) => currentUser.CompanyId is { } id && id != Guid.Empty
        ? id : throw new InvalidOperationException("Current user has no company context.");

    public static Guid RequireEmployeeId(ICurrentUser currentUser) => currentUser.EmployeeId is { } id && id != Guid.Empty
        ? id : throw new InvalidOperationException("Current user has no employee context.");

    public static bool CanManage(ICurrentUserPermissionService permissions) =>
        permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders);
}
