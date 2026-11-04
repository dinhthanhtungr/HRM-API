using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>Quyền thao tác Sale; caller vẫn phải lọc hồ sơ bằng write visibility, company và active.</summary>
internal static class SampleSaleWorkflowAccess
{
    internal static Guid[] GetAllowedOwnerIds(ICurrentUser user, ICurrentUserPermissionService permissions, ViewerScope scope)
    {
        if (!user.IsAuthenticated || user.EmployeeId is not { } employee || employee == Guid.Empty ||
            user.CompanyId is not { } company || company == Guid.Empty ||
            scope.CompanyId != company || scope.EmployeeId != employee ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow)) return [];

        // Quyền xem toàn công ty không tự mở rộng quyền làm thay ngoài nhóm đang quản lý.
        return permissions.HasPermission(ApplicationPermissions.PLM.ManageManagedSampleSaleWorkflow)
            ? scope.EmployeeIdsInScope.Where(id => id != Guid.Empty).Append(employee).Distinct().ToArray()
            : [employee];
    }

    internal static bool CanActFor(ICurrentUser user, ICurrentUserPermissionService permissions, Guid? ownerId, ViewerScope scope)
        => ownerId is { } owner && owner != Guid.Empty && GetAllowedOwnerIds(user, permissions, scope).Contains(owner);
}
