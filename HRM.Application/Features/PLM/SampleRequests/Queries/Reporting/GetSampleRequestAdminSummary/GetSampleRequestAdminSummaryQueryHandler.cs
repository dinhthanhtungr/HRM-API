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
        var records = SampleRequestAdminReportQuery.Build(source, db);
        var start = request.FromDate.ToDateTime(TimeOnly.MinValue);
        var end = request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var received = await Counts(records, SampleRequestAdminMetric.Received, start, end, ct);
        var completed = await Counts(records, SampleRequestAdminMetric.Completed, start, end, ct);
        var open = await Counts(records, SampleRequestAdminMetric.Open, start, end, ct);
        var cancelled = await Counts(records, SampleRequestAdminMetric.Cancelled, start, end, ct);
        var unknown = await records.Where(x => x.CreatedDate < end &&
            (x.Status == "Completed" && x.CompletedDate == null || x.Status == "Cancelled" && x.CancelledDate == null))
            .GroupBy(x => x.BranchId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var branches = await records.Where(x => x.CreatedDate < end)
            .Select(x => new { x.BranchId, Code = x.BranchCode, Name = x.BranchName })
            .Distinct().ToListAsync(ct);
        var rows = branches.OrderBy(x => x.Name).Select(x => new SampleRequestAdminSummaryRowDto
        {
            BranchId = x.BranchId, BranchCode = x.Code, BranchName = x.Name,
            ReceivedCount = received.GetValueOrDefault(x.BranchId), CompletedCount = completed.GetValueOrDefault(x.BranchId),
            OpenCount = open.GetValueOrDefault(x.BranchId), CancelledCount = cancelled.GetValueOrDefault(x.BranchId),
            UnknownTerminalDateCount = unknown.GetValueOrDefault(x.BranchId)
        }).ToArray();
        return new SampleRequestAdminSummaryDto
        {
            FromDate = request.FromDate, ToDate = request.ToDate, Rows = rows,
            Totals = new SampleRequestAdminSummaryRowDto
            {
                ReceivedCount = rows.Sum(x => x.ReceivedCount), CompletedCount = rows.Sum(x => x.CompletedCount),
                OpenCount = rows.Sum(x => x.OpenCount), CancelledCount = rows.Sum(x => x.CancelledCount),
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
