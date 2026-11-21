using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.PLPU;

namespace HRM.Application.Features.Reports.CompanyOverview;

public static class CompanyOverviewAccess
{
    public static bool CanRead(ICurrentUser user, ICurrentUserPermissionService permissions, Guid? companyId) =>
        user.IsInAnyRole(ApplicationRoleSets.Reports.SalesDashboardReaders)
        && PLPUReportAccess.CanRead(user, permissions, companyId);
}
