using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal static class SampleRequestWorkQueueAccess
{
    public static bool CanRead(ICurrentUser user, SampleRequestWorkAudience audience)
        => user.IsAuthenticated && user.CompanyId.HasValue &&
           (user.IsInRole(ApplicationRoles.Admin) || (audience switch
           {
               SampleRequestWorkAudience.Sale => user.IsInRole(ApplicationRoles.Sales.SaleUser),
               SampleRequestWorkAudience.Lab => user.IsInRole(ApplicationRoles.Lab.LabUser) || user.IsInRole(ApplicationRoles.Lab.LabAdmin),
               SampleRequestWorkAudience.LabAdmin => user.IsInRole(ApplicationRoles.Lab.LabAdmin),
               _ => false
           }));
}
