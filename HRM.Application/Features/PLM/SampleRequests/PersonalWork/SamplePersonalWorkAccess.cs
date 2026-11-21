using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.PersonalWork;

internal static class SamplePersonalWorkAccess
{
    public static bool HasActor(ICurrentUser user) => user.IsAuthenticated &&
        user.CompanyId is { } company && company != Guid.Empty &&
        user.EmployeeId is { } employee && employee != Guid.Empty;

    public static async Task<IQueryable<SampleRequest>> ReadableAsync(IPLMDbContext db,
        ICurrentUser user, ICustomerVisibilityService visibility, CancellationToken ct)
    {
        var scope = await visibility.BuildReadScopeAsync(ct);
        scope = await SampleWorkPlanningVisibility.ForPlanningAsync(scope, user, db.Employees, ct);
        return visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(),
            db.Customers.AsNoTracking(), scope).Where(x => x.CompanyId == user.CompanyId && x.IsActive);
    }
}
