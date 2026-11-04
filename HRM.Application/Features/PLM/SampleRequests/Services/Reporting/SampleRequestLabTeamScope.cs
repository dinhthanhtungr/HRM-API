using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

public sealed record SampleLabTeamContext(string[] MemberTeams, bool CanViewAll, SampleLabTeamFilter DefaultFilter);
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SampleLabTeamFilter>))]
public enum SampleLabTeamFilter { Mine, All, Rd, Color, Unassigned }

public sealed class SampleRequestLabTeamScope(ICRMReadDbContext db, ICurrentUser user)
{
    public async Task<SampleLabTeamContext> ReadAsync(CancellationToken ct)
    {
        if (!SampleRequestWorkQueueAccess.CanRead(user, SampleRequestWorkAudience.Lab) ||
            user.CompanyId is not { } company || company == Guid.Empty ||
            user.EmployeeId is not { } employee || employee == Guid.Empty)
            throw new UnauthorizedAccessException("Lab work requires an authenticated company employee.");
        var active = await db.Employees.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.EmployeeId == employee && x.IsActive, ct);
        if (!active) throw new UnauthorizedAccessException("Current employee is not active.");
        var teams = await MemberTeams(db.MemberInGroups.AsNoTracking(), company, employee).ToArrayAsync(ct);
        // Team membership chooses the initial view; access is enforced by the queue and company scope.
        return new(teams, true, teams.Length == 0 ? SampleLabTeamFilter.All : SampleLabTeamFilter.Mine);
    }

    public async Task<IQueryable<SampleRequest>> ApplyAsync(IQueryable<SampleRequest> query, SampleLabTeamFilter? filter, CancellationToken ct)
        => Apply(query, await ReadAsync(ct), filter);

    internal static IQueryable<string> MemberTeams(IQueryable<HRM.Domain.Entities.CompanySchema.MemberInGroup> members, Guid company, Guid employee)
        => members.Where(x => x.IsActive && x.Profile == employee && x.Group.CompanyId == company &&
            (x.Group.GroupType == SampleRequestLabTeamRules.Rd || x.Group.GroupType == SampleRequestLabTeamRules.Color))
            .Select(x => x.Group.GroupType!).Distinct();

    internal static IQueryable<SampleRequest> Apply(IQueryable<SampleRequest> query, SampleLabTeamContext context, SampleLabTeamFilter? filter)
    {
        var selected = filter ?? context.DefaultFilter;
        if (!Enum.IsDefined(selected)) throw new ArgumentException("Invalid Lab team filter.");
        if (selected == SampleLabTeamFilter.All) return query;
        var rd = selected == SampleLabTeamFilter.Rd || selected == SampleLabTeamFilter.Mine && context.MemberTeams.Contains(SampleRequestLabTeamRules.Rd);
        var color = selected == SampleLabTeamFilter.Color || selected == SampleLabTeamFilter.Mine && context.MemberTeams.Contains(SampleRequestLabTeamRules.Color);
        var rdCodes = SampleRequestLabTeamRules.RdCategories;
        var colorCodes = SampleRequestLabTeamRules.ColorCategories;
        if (selected == SampleLabTeamFilter.Unassigned)
            return query.Where(x => x.Product.Category == null || x.Product.Category.ExternalId == null ||
                (!rdCodes.Contains(x.Product.Category.ExternalId.Trim().ToUpper()) && !colorCodes.Contains(x.Product.Category.ExternalId.Trim().ToUpper())));
        return query.Where(x => x.Product.Category != null && x.Product.Category.ExternalId != null &&
            (rd && rdCodes.Contains(x.Product.Category.ExternalId.Trim().ToUpper()) ||
             color && colorCodes.Contains(x.Product.Category.ExternalId.Trim().ToUpper())));
    }
}
