using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.PLM.Shared.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

internal sealed class GetSampleRequestAdminSummaryQueryHandler(IPLMReadDbContext db, ICurrentUser user,
    HRM.Application.Abstractions.Persistence.CRM.CustomerCare.ICRMReadDbContext crm,
    ICustomerVisibilityService visibility) : IRequestHandler<GetSampleRequestAdminSummaryQuery, SampleRequestAdminSummaryDto>
{
    public async Task<SampleRequestAdminSummaryDto> Handle(GetSampleRequestAdminSummaryQuery request, CancellationToken ct)
    {
        if (!SampleRequestReportAccess.CanRead(user))
            throw new UnauthorizedAccessException("Sample reporting requires an authenticated employee and company.");
        var company = user.CompanyId!.Value;
        if (!SampleRequestAdminReportQuery.IsValidPeriod(request.FromDate, request.ToDate))
            throw new ArgumentException("Invalid report period.");
        var scope = await visibility.BuildReadScopeAsync(ct);
        var source = visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking()
            .Where(x => x.CompanyId == company && x.IsActive), db.Customers.AsNoTracking(), scope)
            .Where(x => x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);
        var records = SampleRequestAdminReportQuery.Build(source, db, crm.ProductPricingVersions.AsNoTracking());
        var start = request.FromDate.ToDateTime(TimeOnly.MinValue);
        var end = request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var received = await Counts(records, SampleRequestAdminMetric.Received, start, end, ct);
        var completionGroups = await SampleRequestAdminReportQuery.Filter(records, SampleRequestAdminMetric.Completed, start, end)
            .GroupBy(x => new { x.BranchId, x.LabCompletedKind })
            .Select(g => new { g.Key.BranchId, Kind = g.Key.LabCompletedKind, Count = g.Count() }).ToListAsync(ct);
        var completed = completionGroups.GroupBy(x => x.BranchId).ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        var priceAccepted = await Counts(records, SampleRequestAdminMetric.PriceAccepted, start, end, ct);
        var chipAccepted = await Counts(records, SampleRequestAdminMetric.ChipAccepted, start, end, ct);
        var sampleAccepted = await Counts(records, SampleRequestAdminMetric.SampleAccepted, start, end, ct);
        var open = await Counts(records, SampleRequestAdminMetric.Open, start, end, ct);
        var inProgress = await Counts(records, SampleRequestAdminMetric.InProgress, start, end, ct);
        var pending = await Counts(records, SampleRequestAdminMetric.Pending, start, end, ct);
        var declined = await Counts(records, SampleRequestAdminMetric.LabDeclined, start, end, ct);
        var cancelled = await Counts(records, SampleRequestAdminMetric.Cancelled, start, end, ct);
        var unknown = await records.Where(x => x.CreatedDate < end &&
            (x.Status == "Completed" && x.CompletedDate == null || x.Status == "Cancelled" && x.CancelledDate == null || x.Status == "LabDeclined" && x.LabDeclinedDate == null))
            .GroupBy(x => x.BranchId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var branches = await records.Where(x => x.CreatedDate < end)
            .Select(x => new { x.BranchId, Code = x.BranchCode, Name = x.BranchName })
            .Distinct().ToListAsync(ct);
        var rows = branches.OrderBy(x => x.Name).Select(x => new SampleRequestAdminSummaryRowDto
        {
            BranchId = x.BranchId, BranchCode = x.Code, BranchName = x.Name,
            CompletedBreakdown = completionGroups.Where(g => g.BranchId == x.BranchId).ToDictionary(g => g.Kind, g => g.Count),
            PriceAcceptedCount = priceAccepted.GetValueOrDefault(x.BranchId),
            ChipAcceptedCount = chipAccepted.GetValueOrDefault(x.BranchId),
            SampleAcceptedCount = sampleAccepted.GetValueOrDefault(x.BranchId),
            ReceivedCount = received.GetValueOrDefault(x.BranchId), CompletedCount = completed.GetValueOrDefault(x.BranchId),
            LabDeclinedCount = declined.GetValueOrDefault(x.BranchId), OpenCount = open.GetValueOrDefault(x.BranchId), CancelledCount = cancelled.GetValueOrDefault(x.BranchId),
            InProgressCount = inProgress.GetValueOrDefault(x.BranchId), PendingCount = pending.GetValueOrDefault(x.BranchId),
            UnknownTerminalDateCount = unknown.GetValueOrDefault(x.BranchId)
        }).ToArray();
        var resultsAsOf = DateTime.Now;
        var activities = await SampleActivityReportReader.ReadAsync(
            SampleActivityQuery.Build(source, db, crm.ProductPricingVersions.AsNoTracking(), start, end, resultsAsOf),
            db, crm, company, resultsAsOf, ct);
        return new SampleRequestAdminSummaryDto
        {
            Activities = new SampleActivityReportDto
            {
                ResultsAsOf = activities.ResultsAsOf, Totals = activities.Totals, Employees = activities.Employees,
                Branches = branches.OrderBy(x => x.Name).Select(branch => activities.Branches.FirstOrDefault(x => x.BranchId == branch.BranchId)
                    ?? new SampleActivityGroupDto { BranchId = branch.BranchId, BranchName = branch.Name ?? branch.Code })
                    .Concat(activities.Branches.Where(activity => !branches.Any(branch => branch.BranchId == activity.BranchId))).ToArray()
            },
            FromDate = request.FromDate, ToDate = request.ToDate, Rows = rows,
            CreatorRows = await SampleRequestCreatorReport.ReadAsync(db, crm, company, records, start, end, ct),
            Totals = new SampleRequestAdminSummaryRowDto
            {
                CompletedBreakdown = completionGroups.GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => g.Sum(x => x.Count)),
                PriceAcceptedCount = rows.Sum(x => x.PriceAcceptedCount),
                ChipAcceptedCount = rows.Sum(x => x.ChipAcceptedCount),
                SampleAcceptedCount = rows.Sum(x => x.SampleAcceptedCount),
                ReceivedCount = rows.Sum(x => x.ReceivedCount), CompletedCount = rows.Sum(x => x.CompletedCount),
                LabDeclinedCount = rows.Sum(x => x.LabDeclinedCount), OpenCount = rows.Sum(x => x.OpenCount), CancelledCount = rows.Sum(x => x.CancelledCount),
                InProgressCount = rows.Sum(x => x.InProgressCount), PendingCount = rows.Sum(x => x.PendingCount),
                UnknownTerminalDateCount = rows.Sum(x => x.UnknownTerminalDateCount)
            }
        };
    }

    private static Task<Dictionary<Guid, int>> Counts(IQueryable<SampleRequestAdminReportRecord> records,
        SampleRequestAdminMetric metric, DateTime start, DateTime end, CancellationToken ct)
        => SampleRequestAdminReportQuery.Filter(records, metric, start, end)
            .GroupBy(x => x.BranchId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
}
