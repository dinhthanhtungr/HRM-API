using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.Employees.Administration;

internal static class EmployeeAdministrationRules
{
    public const int MaximumRoleNameLength = 64;

    public static bool CanManageEmployees(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(
            ApplicationRoleSets.EmployeeAdministration.EmployeeManagers);

    public static bool CanManageAllCompanies(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(
            ApplicationRoleSets.EmployeeAdministration.GlobalCompanyManagers);

    public static bool CanManageRoleTypes(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(
            ApplicationRoleSets.EmployeeAdministration.RoleTypeManagers);

    public static bool IsPrivilegedRole(string roleName)
        => ApplicationRoleSets.EmployeeAdministration.PrivilegedRoles
            .Contains(roleName, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidRoleName(string roleName)
        => roleName.Length is > 0 and <= MaximumRoleNameLength &&
           roleName.All(character =>
               char.IsLetterOrDigit(character) ||
               character is '.' or '_' or '-');
}
