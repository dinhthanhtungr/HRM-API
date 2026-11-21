using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestTracking;
using HRM.Application.Features.PLM.SampleRequests.Services.Requests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.ListSampleRequestTracking;

internal sealed class ListSampleRequestTrackingQueryHandler(
    IPLMReadDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions)
    : IRequestHandler<ListSampleRequestTrackingQuery, PagedResult<SampleRequestTrackingDto>?>
{
    private const int PageSize = 25;

    public async Task<PagedResult<SampleRequestTrackingDto>?> Handle(
        ListSampleRequestTrackingQuery request, CancellationToken cancellationToken)
    {
        if (SampleRequestTrackingReader.ResolveCompany(user, permissions) is not { } companyId)
            return null;

        var query = SampleRequestTrackingReader.VisibleRequests(db, companyId);
        var keyword = request.Keyword?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(sample => sample.ExternalId.ToLower().Contains(keyword) ||
                (sample.Product.Code != null && sample.Product.Code.ToLower().Contains(keyword)) ||
                (sample.Product.Name != null && sample.Product.Name.ToLower().Contains(keyword)));
        }

        var total = await query.CountAsync(cancellationToken);
        var page = Math.Clamp(request.PageNumber, 1, Math.Max(1, (int)Math.Ceiling(total / (double)PageSize)));
        var rows = query.OrderByDescending(sample => sample.CreatedDate)
            .ThenBy(sample => sample.SampleRequestId)
            .Skip((page - 1) * PageSize)
            .Take(PageSize);
        var items = await SampleRequestTrackingReader.Project(rows).ToListAsync(cancellationToken);
        return new PagedResult<SampleRequestTrackingDto>(items, total, page, PageSize);
    }
}
