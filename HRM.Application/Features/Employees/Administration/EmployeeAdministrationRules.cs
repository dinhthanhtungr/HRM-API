using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Application.Features.Employees.Administration;

internal static class EmployeeAdministrationRules
{
    public const int MaximumRoleNameLength = 64;

    public static bool CanManageEmployees(ICurrentUser currentUser)
        => currentUser.IsAuthenticated && currentUser.IsInAnyRole(
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

    public static IQueryable<Employee> ScopeEmployees(IQueryable<Employee> employees, ICurrentUser user)
    {
        if (!user.IsAuthenticated) return employees.Where(_ => false);
        if (CanManageAllCompanies(user)) return employees;
        if (!user.CompanyId.HasValue) return employees.Where(_ => false);
        return employees.Where(employee => employee.CompanyId == user.CompanyId.Value);
    }
}
