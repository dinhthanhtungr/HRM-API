using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal static class SampleWorkSaleFilter
{
    internal static IQueryable<SampleRequest> Apply(IQueryable<SampleRequest> source,
        IQueryable<MemberInGroup> members, Guid company, Guid[]? groups, Guid[]? employees)
    {
        source = source.Where(x => x.CompanyId == company && x.IsActive);
        if (groups?.Length > 100 || employees?.Length > 100 ||
            groups?.Contains(Guid.Empty) == true || employees?.Contains(Guid.Empty) == true)
            throw new ArgumentException("Invalid Sale filters.");
        if (employees is { Length: > 0 })
            source = source.Where(x => employees.Contains(x.ManagerBy));
        if (groups is { Length: > 0 })
            source = source.Where(x => members.Any(m => m.IsActive && m.Profile == x.ManagerBy &&
                groups.Contains(m.GroupId) && m.Group.CompanyId == company &&
                (m.Group.GroupType == "CMR" || m.Group.GroupType != null && m.Group.GroupType.StartsWith("CMR.")) &&
                m.ProfileNavigation != null && m.ProfileNavigation.IsActive && m.ProfileNavigation.CompanyId == company));
        return source;
    }
}
