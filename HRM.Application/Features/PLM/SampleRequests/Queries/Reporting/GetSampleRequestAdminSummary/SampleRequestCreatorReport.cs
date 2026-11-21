using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

internal static class SampleRequestCreatorReport
{
    public static async Task<IReadOnlyList<SampleRequestCreatorSummaryRowDto>> ReadAsync(
        IPLMReadDbContext db, ICRMReadDbContext crm, Guid company,
        IQueryable<SampleRequestAdminReportRecord> records, DateTime start, DateTime end, CancellationToken ct)
    {
        var roster = await crm.MemberInGroups.AsNoTracking()
            .Where(m => m.IsActive && m.Group.CompanyId == company && m.ProfileNavigation != null &&
                m.ProfileNavigation.CompanyId == company && m.ProfileNavigation.IsActive &&
                (m.Group.GroupType == SampleRequestLabTeamRules.Rd || m.Group.GroupType == SampleRequestLabTeamRules.Color))
            .Select(m => new { Team = m.Group.GroupType!, EmployeeId = m.Profile!.Value, EmployeeName = m.ProfileNavigation!.FullName })
            .Distinct().ToListAsync(ct);
        var employees = roster.Select(x => x.EmployeeId).Distinct().ToArray();
        if (employees.Length == 0) return [];
        var completed = await Contributions(SampleRequestAdminReportQuery.Filter(records, SampleRequestAdminMetric.Completed, start, end),
                db.Formulas.AsNoTracking(), company, employees)
            .GroupBy(x => new { x.EmployeeId, x.Kind })
            .Select(g => new { g.Key.EmployeeId, g.Key.Kind, Count = g.Count() }).ToListAsync(ct);
        var working = await Counts(SampleRequestAdminReportQuery.Filter(records, SampleRequestAdminMetric.InProgress, start, end));
        return roster.OrderBy(x => x.Team).ThenBy(x => x.EmployeeName).Select(x => new SampleRequestCreatorSummaryRowDto
        {
            Team = x.Team, EmployeeId = x.EmployeeId, EmployeeName = x.EmployeeName,
            CompletedCount = completed.Where(g => g.EmployeeId == x.EmployeeId).Sum(g => g.Count),
            CompletedBreakdown = completed.Where(g => g.EmployeeId == x.EmployeeId).ToDictionary(g => g.Kind, g => g.Count),
            InProgressCount = working.GetValueOrDefault(x.EmployeeId)
        }).ToArray();

        Task<Dictionary<Guid, int>> Counts(IQueryable<SampleRequestAdminReportRecord> samples)
            => Contributions(samples, db.Formulas.AsNoTracking(), company, employees)
                .GroupBy(x => x.EmployeeId).Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Count, ct);
    }

    internal sealed record Contribution
    {
        public string Kind { get; init; } = "other";
        public Guid SampleRequestId { get; init; }
        public Guid EmployeeId { get; init; }
    }

    internal static IQueryable<Contribution> Contributions(IQueryable<SampleRequestAdminReportRecord> samples,
        IQueryable<HRM.Domain.Entities.SampleRequestSchema.Formula> formulas, Guid company, Guid[] employees)
        => (from sample in samples
            join formula in formulas.Where(f => f.IsActive && f.CompanyId == company &&
                f.CreatedBy.HasValue && employees.Contains(f.CreatedBy.Value)) on sample.ProductId equals formula.ProductId
            select new Contribution { SampleRequestId = sample.SampleRequestId, EmployeeId = formula.CreatedBy!.Value, Kind = sample.LabCompletedKind }).Distinct();
}
