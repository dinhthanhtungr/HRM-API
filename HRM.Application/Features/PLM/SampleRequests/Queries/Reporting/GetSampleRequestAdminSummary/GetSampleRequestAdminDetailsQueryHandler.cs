using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

internal sealed class GetSampleRequestAdminDetailsQueryHandler(ICurrentUser user, ISender sender,
    IPLMReadDbContext db, ICustomerVisibilityService visibility, SampleWorkPreviewSession? preview = null,
    ILogger<GetSampleRequestAdminDetailsQueryHandler>? logger = null,
    HRM.Application.Abstractions.Persistence.CRM.CustomerCare.ICRMReadDbContext? crm = null)
    : IRequestHandler<GetSampleRequestAdminDetailsQuery, PagedResult<SampleRequestSampleTrialReportDto>>
{
    public async Task<PagedResult<SampleRequestSampleTrialReportDto>> Handle(GetSampleRequestAdminDetailsQuery request, CancellationToken ct)
    {
        using var timings = new SampleWorkQueryTimings(logger ?? NullLogger<GetSampleRequestAdminDetailsQueryHandler>.Instance, true, "admin_details");
        if (!SampleRequestReportAccess.CanRead(user))
            throw new UnauthorizedAccessException("Sample reporting requires an authenticated employee and company.");
        if (!SampleRequestAdminReportQuery.IsValidPeriod(request.FromDate, request.ToDate) || !Enum.IsDefined(request.Metric))
            throw new ArgumentException("Invalid report period or metric.");
        // Search keeps its existing trial/formula keyword semantics. The drawer currently
        // sends no keyword: count and page request IDs before computing workflow details.
        if (request.NormalizedKeyword != null && request.CreatorEmployeeId == null)
            return await sender.Send(new GetSampleRequestSampleTrialsQuery
            {
                DailyWorkDate = request.ToDate, DailyWorkView = SampleRequestDailyView.All,
                AdminPeriodFrom = request.FromDate, AdminPeriodTo = request.ToDate,
                AdminMetric = request.Metric, AdminBranchId = request.BranchId,
                Keyword = request.Keyword, PageNumber = request.PageNumber, PageSize = request.PageSize
            }, ct);
        var scope = await visibility.BuildReadScopeAsync(ct);
        var source = visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking().Where(x => x.IsActive && x.CompanyId == user.CompanyId),
            db.Customers.AsNoTracking(), scope)
            .Where(x => x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId && x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);
        if (preview?.ActorReadScope is { } actorScope)
            source = visibility.ApplySampleRequestVisibility(source, db.Customers.AsNoTracking(), actorScope);
        if (request.CreatorEmployeeId is { } creator)
        {
            if (request.Metric is not (SampleRequestAdminMetric.Completed or SampleRequestAdminMetric.InProgress or
                SampleRequestAdminMetric.CompletedQuotation or SampleRequestAdminMetric.CompletedChip or
                SampleRequestAdminMetric.CompletedSample or SampleRequestAdminMetric.CompletedOther))
                throw new ArgumentException("Creator reporting supports completed and in-progress samples only.");
            source = source.Where(x => db.Formulas.Any(f => f.IsActive && f.CompanyId == x.CompanyId &&
                f.ProductId == x.ProductId && f.CreatedBy == creator));
        }
        var records = SampleRequestAdminReportQuery.Filter(SampleRequestAdminReportQuery.Build(source, db, crm?.ProductPricingVersions.AsNoTracking()), request.Metric,
            request.FromDate.ToDateTime(TimeOnly.MinValue), request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (request.BranchId is { } branch) records = records.Where(x => x.BranchId == branch);
        timings.Mark("scope");
        var count = await records.TagWith("SampleAdminDetails.Count").CountAsync(ct);
        timings.Mark("count");
        var ids = await SelectPageIds(records, request.NormalizedPageNumber, request.NormalizedPageSize)
            .TagWith("SampleAdminDetails.PageIds").ToArrayAsync(ct);
        timings.Mark("page");
        if (ids.Length == 0) return new([], count, request.NormalizedPageNumber, request.NormalizedPageSize);
        var page = await sender.Send(new GetSampleRequestDailyWorkQuery
        {
            Date = request.ToDate, View = SampleRequestDailyView.All,
            SampleRequestIds = ids, PageNumber = 1, PageSize = ids.Length, SkipUnread = true,
            ReadSelectedReportPage = true
        }, ct);
        timings.Mark("payload");
        var order = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return new(page.Items.OrderBy(x => order[x.SampleRequestId]).ToArray(), count, request.NormalizedPageNumber, request.NormalizedPageSize);
    }

    internal static IQueryable<Guid> SelectPageIds(IQueryable<SampleRequestAdminReportRecord> records, int page, int size)
        => records
            .OrderByDescending(x => x.CreatedDate).ThenBy(x => x.ExternalId).ThenBy(x => x.SampleRequestId)
            .Skip((page - 1) * size).Take(size).Select(x => x.SampleRequestId);
}
