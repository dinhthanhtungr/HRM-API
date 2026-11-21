using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.Shared.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleActivityDetails;

internal sealed class GetSampleActivityDetailsQueryHandler(IPLMReadDbContext db, ICRMReadDbContext crm,
    ICurrentUser user, ICustomerVisibilityService visibility)
    : IRequestHandler<GetSampleActivityDetailsQuery, PagedResult<SampleActivityDetailDto>>
{
    public async Task<PagedResult<SampleActivityDetailDto>> Handle(GetSampleActivityDetailsQuery request, CancellationToken ct)
    {
        if (!SampleRequestReportAccess.CanRead(user)) throw new UnauthorizedAccessException("Sample report access required.");
        if (!SampleRequestAdminReportQuery.IsValidPeriod(request.FromDate, request.ToDate) || !request.HasValidFilters())
            throw new ArgumentException("Invalid activity report filter.");
        var company = user.CompanyId!.Value;
        var scope = await visibility.BuildReadScopeAsync(ct);
        var visible = visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive),
            db.Customers.AsNoTracking(), scope).Where(x => x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);
        var rows = SampleActivityQuery.Build(visible, db, crm.ProductPricingVersions.AsNoTracking(),
            request.FromDate.ToDateTime(TimeOnly.MinValue), request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTime.Now);
        rows = Filter(rows, crm, company, request);
        var count = await rows.CountAsync(ct);
        // The response filter lists activities carrying replies; the sum is exposed per row,
        // while pagination remains over activities, not over synthetic copies of a TP.
        var items = await SelectPage(rows, visible, db, company, request.NormalizedPageNumber, request.NormalizedPageSize)
            .TagWith("SampleActivityReport.DetailsPage").ToArrayAsync(ct);
        return new(items, count, request.NormalizedPageNumber, request.NormalizedPageSize);
    }

    internal static IQueryable<SampleActivityDetailDto> SelectPage(IQueryable<SampleActivityRow> rows,
        IQueryable<SampleRequest> visible, IPLMReadDbContext db, Guid company, int page, int size)
        => from activity in rows.OrderByDescending(x => x.CompletedAt).ThenBy(x => x.RequestId).ThenBy(x => x.Kind).ThenBy(x => x.Id)
                .Skip((page - 1) * size).Take(size)
            join sample in visible on activity.RequestId equals sample.SampleRequestId
            select new SampleActivityDetailDto
            {
                ActivityId = activity.Id, SampleRequestId = activity.RequestId, RequestCode = sample.ExternalId,
                ProductName = sample.Product.Name ?? "", ColourCode = sample.Product.ColourCode,
                FormulaCode = db.Formulas.Where(f => f.CompanyId == company && f.ProductId == sample.ProductId && f.FormulaId == activity.FormulaId)
                    .Select(f => f.ExternalId).FirstOrDefault(), TrialNo = activity.TrialNo, Kind = activity.Kind,
                CompletedAt = activity.CompletedAt, Outcome = activity.Outcome, Responses = activity.Responses,
                LatestResponseAt = activity.LatestResponseAt,
                EmployeeName = db.Employees.Where(e => e.CompanyId == company && e.EmployeeId == activity.EmployeeId)
                    .Select(e => e.FullName).FirstOrDefault()
            };

    internal static IQueryable<SampleActivityRow> Filter(IQueryable<SampleActivityRow> rows, ICRMReadDbContext crm, Guid company,
        GetSampleActivityDetailsQuery request)
    {
        if (request.Kind != null) rows = rows.Where(x => x.Kind == request.Kind);
        if (request.Outcome == "responses") rows = rows.Where(x => x.Responses > 0);
        else if (request.Outcome != null) rows = rows.Where(x => x.Outcome == request.Outcome);
        if (request.BranchId.HasValue) rows = rows.Where(x => x.BranchId == request.BranchId);
        if (request.EmployeeId.HasValue) rows = request.EmployeeId == Guid.Empty
            ? rows.Where(x => x.EmployeeId == null) : rows.Where(x => x.EmployeeId == request.EmployeeId);
        if (request.Team != null)
        {
            var memberships = crm.MemberInGroups.Where(m => m.IsActive && m.Group.CompanyId == company && m.ProfileNavigation != null &&
                m.ProfileNavigation.CompanyId == company && m.ProfileNavigation.IsActive && (m.Group.GroupType == SampleRequestLabTeamRules.Rd || m.Group.GroupType == SampleRequestLabTeamRules.Color))
                .Select(m => new { EmployeeId = m.Profile!.Value, Team = m.Group.GroupType! }).Distinct();
            var single = memberships.GroupBy(x => x.EmployeeId).Where(g => g.Count() == 1).Select(g => g.Key);
            rows = request.Team == "unassigned" ? rows.Where(x => x.EmployeeId == null || !memberships.Any(m => m.EmployeeId == x.EmployeeId)) :
                request.Team == "multiple" ? rows.Where(x => x.EmployeeId != null && memberships.Any(m => m.EmployeeId == x.EmployeeId) && !single.Contains(x.EmployeeId.Value)) :
                rows.Where(x => x.EmployeeId != null && single.Contains(x.EmployeeId.Value) && memberships.Any(m => m.EmployeeId == x.EmployeeId && m.Team == request.Team));
        }
        return rows;
    }
}
