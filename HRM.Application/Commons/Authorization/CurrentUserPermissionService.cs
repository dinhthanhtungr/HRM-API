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
