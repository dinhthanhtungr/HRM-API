using HRM.Application.Abstractions.Security;

namespace HRM.Application.Commons.Authorization;

internal sealed class CurrentUserPermissionService : ICurrentUserPermissionService
{
    private readonly ICurrentUser _currentUser;

    public CurrentUserPermissionService(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission) || !_currentUser.IsAuthenticated)
        {
            return false;
        }

        // Developer can confirm CRM sending even with an older explicit permission token.
        if (string.Equals(permission, ApplicationPermissions.Quotations.ConfirmSent, StringComparison.OrdinalIgnoreCase) &&
            _currentUser.IsInRole(ApplicationRoles.Developer))
            return true;

        // Quyền mua hàng của PLPUUser được cấp theo role, kể cả token explicit chưa có claim.
        if (string.Equals(permission, ApplicationPermissions.Purchasing.ManagePurchaseOrders, StringComparison.OrdinalIgnoreCase) &&
            _currentUser.IsInRole(ApplicationRoles.Production.PLPUUser))
            return true;

        // Quan sát hội thoại công ty gắn với role Developer, tương thích cả token explicit cũ.
        if (string.Equals(permission, ApplicationPermissions.InternalMail.ObserveCompanyConversations, StringComparison.OrdinalIgnoreCase))
            return _currentUser.IsInAnyRole(ApplicationRoleSets.InternalMail.CompanyConversationObservers);
        if (string.Equals(permission, ApplicationPermissions.InternalMail.SendCompanyMessages, StringComparison.OrdinalIgnoreCase))
            return _currentUser.IsInAnyRole(ApplicationRoleSets.InternalMail.CompanyMessageSenders);

        // GROUP-STORAGE-TRANSITION: synchronization-api — preserve old Developer tokens; a claim alone cannot grant this maintenance action.
        if (string.Equals(permission, ApplicationPermissions.InternalMail.SynchronizeGroupStorage, StringComparison.OrdinalIgnoreCase))
            return _currentUser.IsInAnyRole(ApplicationRoleSets.InternalMail.GroupStorageAdministrators);

        if (_currentUser.HasExplicitPermissionSet)
        {
            return _currentUser.Permissions.Contains(
                permission,
                StringComparer.OrdinalIgnoreCase);
        }

        // Compatibility path for access tokens issued before role permissions were seeded.
        return _currentUser.IsInAnyRole(ApplicationPermissionRoleSets.GetRoles(permission));
    }
}
