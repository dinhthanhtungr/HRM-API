using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal static class SampleRequestWorkQueueAccess
{
    public static bool CanRead(ICurrentUser user, SampleRequestWorkAudience audience)
        => user.IsAuthenticated && user.CompanyId.HasValue &&
           (user.IsInRole(ApplicationRoles.Admin) || (audience switch
           {
               SampleRequestWorkAudience.Sale => user.IsInAnyRole(ApplicationRoleSets.PLM.SampleSaleWorkReaders),
               SampleRequestWorkAudience.Lab => user.IsInRole(ApplicationRoles.Developer) || user.IsInRole(ApplicationRoles.Lab.LabUser) || user.IsInRole(ApplicationRoles.Lab.LabAdmin),
               SampleRequestWorkAudience.LabAdmin => user.IsInRole(ApplicationRoles.Developer) || user.IsInRole(ApplicationRoles.Lab.LabAdmin),
               _ => false
           }));
}
