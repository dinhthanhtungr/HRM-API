using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetFinishDashboard;

public static class FinishDashboardScope
{
    public const string ReadRoles = "President,Developer,SaleUser,Leader,ACCUser,PLPUUser";
    private static readonly string[] Readers = [ApplicationRoles.President, ApplicationRoles.Developer, ApplicationRoles.Sales.SaleUser, ApplicationRoles.Leader, ApplicationRoles.Accounting.ACCUser, ApplicationRoles.Production.PLPUUser];
    private static readonly string[] CompanyReaders = [ApplicationRoles.President, ApplicationRoles.Developer, ApplicationRoles.Accounting.ACCUser, ApplicationRoles.Production.PLPUUser];
    public static bool CanRead(ICurrentUser user, ICurrentUserPermissionService? permissions = null) =>
        ImsAccountRestrictions.AppliesTo(user)
            ? user.EmployeeId.GetValueOrDefault() != Guid.Empty &&
                permissions?.HasPermission(ApplicationPermissions.Reports.ViewDeliveryFinish) == true
            : user.IsAuthenticated && user.IsInAnyRole(Readers);
    internal static bool IsCompany(ICurrentUser user) => user.IsAuthenticated && user.IsInAnyRole(CompanyReaders);

    internal static bool IsTeam(ICurrentUser user, bool ownOnly) => CanRead(user) && !IsCompany(user) && user.IsInRole(ApplicationRoles.Leader) && !ownOnly;

    internal static IQueryable<Customer> Customers(IQueryable<Customer> customers, ICurrentUser user, DateTime now,
        IQueryable<Guid>? managedGroups = null, bool ownOnly = false,
        ICurrentUserPermissionService? permissions = null)
    {
        if (!CanRead(user, permissions) || user.CompanyId is not { } company || company == Guid.Empty)
            return customers.Where(_ => false);
        var scoped = customers.Where(x => x.CompanyId == company);
        if (ImsAccountRestrictions.AppliesTo(user))
            return scoped.Where(customer => customer.IsActive == true &&
                customer.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
                customer.ExternalId != null &&
                customer.ExternalId.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId);
        if (IsCompany(user)) return scoped;
        if (user.EmployeeId is not { } employee || employee == Guid.Empty)
            return customers.Where(_ => false);
        var team = IsTeam(user, ownOnly);
        managedGroups ??= Array.Empty<Guid>().AsQueryable();
        return scoped.Where(x => x.IsActive == true && (
            x.CustomerAssignments.Any(a => a.IsActive && a.CompanyId == company &&
                (a.EmployeeId == employee || (team && managedGroups.Contains(a.GroupId)))) ||
            x.CustomerClaims.Any(c => c.IsActive && c.CompanyId == company && c.Type == ClaimType.Work && c.ExpiresAt > now &&
                (c.EmployeeId == employee || (team && managedGroups.Contains(c.GroupId))))));
    }
}
