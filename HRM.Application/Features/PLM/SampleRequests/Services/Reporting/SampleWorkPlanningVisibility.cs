using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

/// <summary>Company-wide sample planning only; never changes CRM visibility or grants technical mutation rights.</summary>
internal static class SampleWorkPlanningVisibility
{
    internal static async Task<ViewerScope> ForPlanningAsync(ViewerScope scope, ICurrentUser user,
        IQueryable<Employee> employees, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !user.IsInRole(ApplicationRoles.Lab.LabAdmin) ||
            user.CompanyId is not { } company || company == Guid.Empty || company != scope.CompanyId ||
            user.EmployeeId is not { } employee || employee == Guid.Empty || employee != scope.EmployeeId ||
            !await employees.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.EmployeeId == employee && x.IsActive, ct))
            return scope;
        return scope with { HasFullCustomerView = true, CanViewInternalCustomer = false };
    }
}
