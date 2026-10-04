using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.Employees.Administration;

internal static class RolePermissionAdministrationRules
{
    public static bool CanManage(ICurrentUser user)
        => user.IsAuthenticated && user.IsInAnyRole(
            ApplicationRoleSets.EmployeeAdministration.GlobalCompanyManagers);

    public static bool IsValid(IReadOnlyList<string>? permissions)
        => permissions is not null && permissions.Count <= ApplicationPermissionCatalog.Codes.Count &&
           permissions.All(code => ApplicationPermissionCatalog.Codes.Contains(code, StringComparer.Ordinal)) &&
           permissions.Distinct(StringComparer.Ordinal).Count() == permissions.Count;
}
