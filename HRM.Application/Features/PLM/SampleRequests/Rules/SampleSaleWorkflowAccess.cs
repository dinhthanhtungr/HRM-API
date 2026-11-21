using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>Quyền thao tác Sale; caller vẫn phải lọc hồ sơ bằng write visibility, company và active.</summary>
internal static class SampleSaleWorkflowAccess
{
    internal static bool HasCompanyWideAccess(ICurrentUser user, ViewerScope scope)
        => user.IsAuthenticated && user.IsInRole(ApplicationRoles.Developer) &&
            user.CompanyId is { } company && company != Guid.Empty && company == scope.CompanyId &&
            user.EmployeeId is { } employee && employee != Guid.Empty && employee == scope.EmployeeId;

    internal static Guid[] GetAllowedOwnerIds(ICurrentUser user, ICurrentUserPermissionService permissions, ViewerScope scope)
    {
        if (!user.IsAuthenticated || user.EmployeeId is not { } employee || employee == Guid.Empty ||
            user.CompanyId is not { } company || company == Guid.Empty ||
            scope.CompanyId != company || scope.EmployeeId != employee ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow)) return [];

        // Quyền xem toàn công ty không tự mở rộng quyền làm thay ngoài nhóm đang quản lý.
        var canManageTeam = permissions.HasPermission(ApplicationPermissions.PLM.ManageManagedSampleSaleWorkflow) &&
            (user.IsInRole(ApplicationRoles.Sales.SaleAdmin) || scope.LeaderGroupIds.Count > 0);
        return canManageTeam
            ? scope.EmployeeIdsInScope.Where(id => id != Guid.Empty).Append(employee).Distinct().ToArray()
            : [employee];
    }

    internal static bool CanActFor(ICurrentUser user, ICurrentUserPermissionService permissions, Guid? ownerId, ViewerScope scope)
        => HasCompanyWideAccess(user, scope) ||
            ownerId is { } owner && owner != Guid.Empty && GetAllowedOwnerIds(user, permissions, scope).Contains(owner);

    /// <summary>Cancellation follows current CRM customer ownership, independently of the TP's ManagerBy.</summary>
    internal static IQueryable<Customer> CustomersAllowedForCancellation(IQueryable<Customer> customers,
        ICurrentUser user, ICurrentUserPermissionService permissions, ViewerScope scope)
    {
        if (!user.IsAuthenticated || user.CompanyId != scope.CompanyId || user.EmployeeId != scope.EmployeeId ||
            scope.CompanyId == Guid.Empty || scope.EmployeeId == Guid.Empty)
            return customers.Where(_ => false);
        customers = customers.Where(c => c.CompanyId == scope.CompanyId);
        if (user.IsInAnyRole(ApplicationRoleSets.SuperUsers)) return customers;
        customers = customers.Where(c => c.IsActive == true);
        var owners = GetAllowedOwnerIds(user, permissions, scope);
        var now = scope.Now;
        return customers.Where(c =>
            c.CustomerAssignments.Any(a => a.IsActive && a.CompanyId == scope.CompanyId && owners.Contains(a.EmployeeId)) ||
            (!c.CustomerAssignments.Any(a => a.IsActive && a.CompanyId == scope.CompanyId) &&
             ((c.IsLead && c.CustomerClaims.Any(cl => cl.IsActive && cl.CompanyId == scope.CompanyId &&
                   cl.Type == ClaimType.Work && cl.ExpiresAt > now && owners.Contains(cl.EmployeeId))) ||
              ((!c.IsLead || !c.CustomerClaims.Any(cl => cl.IsActive && cl.CompanyId == scope.CompanyId &&
                    cl.Type == ClaimType.Work && cl.ExpiresAt > now)) &&
               c.CurrentSaleId.HasValue && owners.Contains(c.CurrentSaleId.Value)))));
    }
}
