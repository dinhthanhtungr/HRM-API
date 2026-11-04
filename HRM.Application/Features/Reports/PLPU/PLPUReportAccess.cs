using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
namespace HRM.Application.Features.Reports.PLPU;

public static class PLPUReportAccess
{
    public static bool CanRead(ICurrentUser user, ICurrentUserPermissionService permissions, Guid? companyId) =>
        user.IsAuthenticated && user.CompanyId is { } company && company != Guid.Empty
        && (!companyId.HasValue || companyId == company)
        && user.IsInAnyRole(ApplicationRoleSets.Reports.PlpuReaders)
        && permissions.HasPermission(ApplicationPermissions.Reports.ViewPlpu);

    public static string? Validate(PLPUPurchaseOverviewQuery query)
    {
        if (query.From?.Date > query.To?.Date) return "From must be before or equal to To.";
        if (query.To?.Date == DateTime.MaxValue.Date) return "To is outside the supported date range.";
        if (query.PurchaseOrderId == Guid.Empty || query.SupplierId == Guid.Empty || query.MaterialId == Guid.Empty)
            return "Filter IDs must not be empty.";
        if (query.PageNumber > 1 && (long)(query.PageNumber - 1) * Math.Max(query.PageSize, 15) > int.MaxValue)
            return "Page is outside the supported range.";
        return null;
    }
}
