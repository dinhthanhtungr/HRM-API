using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

internal sealed record SampleActivityAggregate(Guid BranchId, string? BranchName, Guid? EmployeeId,
    string Kind, string Outcome, int Count, int Responses);

internal static class SampleActivityReportReader
{
    internal static async Task<SampleActivityReportDto> ReadAsync(IQueryable<SampleActivityRow> rows,
        IPLMReadDbContext db, ICRMReadDbContext crm, Guid company, DateTime now, CancellationToken ct)
    {
        var counts = await rows.GroupBy(x => new { x.BranchId, x.BranchName, x.EmployeeId, x.Kind, x.Outcome })
            .Select(g => new SampleActivityAggregate(g.Key.BranchId, g.Key.BranchName, g.Key.EmployeeId,
                g.Key.Kind, g.Key.Outcome, g.Count(), g.Sum(x => x.Responses)))
            .TagWith("SampleActivityReport.Aggregate").ToListAsync(ct);
        var ids = counts.Where(x => x.EmployeeId != null).Select(x => x.EmployeeId!.Value).Distinct().ToArray();
        var names = await db.Employees.AsNoTracking().Where(x => x.CompanyId == company && ids.Contains(x.EmployeeId))
            .Select(x => new { x.EmployeeId, x.FullName }).ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, ct);
        var memberships = await crm.MemberInGroups.AsNoTracking().Where(m => m.IsActive && m.Group.CompanyId == company &&
                m.ProfileNavigation != null && m.ProfileNavigation.CompanyId == company && m.ProfileNavigation.IsActive &&
                (m.Group.GroupType == SampleRequestLabTeamRules.Rd || m.Group.GroupType == SampleRequestLabTeamRules.Color))
            .Select(m => new { EmployeeId = m.Profile!.Value, Team = m.Group.GroupType!, Name = m.ProfileNavigation!.FullName }).Distinct().ToListAsync(ct);
        foreach (var member in memberships) names.TryAdd(member.EmployeeId, member.Name);
        var teams = memberships.GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key,
            g => g.Select(x => x.Team).Distinct().Count() == 1 ? g.First().Team : "multiple");
        return new()
        {
            ResultsAsOf = now, Totals = Aggregate(counts),
            Branches = counts.GroupBy(x => new { x.BranchId, x.BranchName }).OrderBy(g => g.Key.BranchName).Select(g => new SampleActivityGroupDto
            { BranchId = g.Key.BranchId, BranchName = g.Key.BranchName, Counts = Aggregate(g) }).ToArray(),
            Employees = counts.Select(x => x.EmployeeId).Concat(memberships.Select(x => (Guid?)x.EmployeeId)).Distinct()
                .OrderBy(id => id.HasValue ? names.GetValueOrDefault(id.Value) : "")
                .Select(id => new SampleActivityGroupDto { EmployeeId = id,
                    EmployeeName = id.HasValue ? names.GetValueOrDefault(id.Value) : null,
                    Team = id.HasValue ? teams.GetValueOrDefault(id.Value, "unassigned") : "unassigned",
                    Counts = Aggregate(counts.Where(x => x.EmployeeId == id)) }).ToArray()
        };
    }

    internal static IReadOnlyDictionary<string, SampleActivityCountsDto> Aggregate(IEnumerable<SampleActivityAggregate> source)
        => source.GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => new SampleActivityCountsDto
        {
            Completed = g.Sum(x => x.Count), Responses = g.Sum(x => x.Responses),
            Accepted = g.Where(x => x.Outcome == "accepted").Sum(x => x.Count),
            Rejected = g.Where(x => x.Outcome == "rejected").Sum(x => x.Count),
            Waiting = g.Where(x => x.Outcome == "waiting").Sum(x => x.Count),
            LabDeclined = g.Where(x => x.Outcome == "labDeclined").Sum(x => x.Count),
            Cancelled = g.Where(x => x.Outcome == "cancelled").Sum(x => x.Count),
            Unknown = g.Where(x => x.Outcome == "unknown").Sum(x => x.Count),
            Optional = g.Where(x => x.Outcome == "optional").Sum(x => x.Count)
        });
}
