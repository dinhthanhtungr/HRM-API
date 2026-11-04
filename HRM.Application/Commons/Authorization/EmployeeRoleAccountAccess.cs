using HRM.Application.Abstractions.Security;

namespace HRM.Application.Commons.Authorization;

public static class EmployeeRoleAccountAccess
{
    private static readonly string[] Viewers =
    [ApplicationRoles.Developer, ApplicationRoles.President,
        ApplicationRoles.HumanResources.HC_HRUser, ApplicationRoles.HumanResources.HCHRUser];

    public static bool CanView(ICurrentUser user)
        => user.IsAuthenticated && user.IsInAnyRole(Viewers);
}
