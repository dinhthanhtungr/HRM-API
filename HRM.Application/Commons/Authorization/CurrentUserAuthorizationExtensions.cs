using HRM.Application.Abstractions.Security;

namespace HRM.Application.Commons.Authorization;

public static class CurrentUserAuthorizationExtensions
{
    public static bool IsInAnyRole(
        this ICurrentUser currentUser,
        IEnumerable<string> roles)
    {
        return roles.Any(currentUser.IsInRole);
    }
}
