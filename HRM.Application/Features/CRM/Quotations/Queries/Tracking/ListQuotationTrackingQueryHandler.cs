using HRM.Application.Commons.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.Tracking;

internal sealed class ListQuotationTrackingQueryHandler(QuotationTrackingReader reader)
    : IRequestHandler<ListQuotationTrackingQuery, PagedResult<QuotationTrackingDto>?>
{
    private const int PageSize = 25;

    public async Task<PagedResult<QuotationTrackingDto>?> Handle(ListQuotationTrackingQuery request, CancellationToken cancellationToken)
    {
        var query = await reader.ReadAsync(cancellationToken);
        if (query is null) return null;

        var count = await query.CountAsync(cancellationToken);
        var page = Math.Clamp(request.PageNumber, 1, Math.Max(1, (int)Math.Ceiling(count / (double)PageSize)));
        var rows = query.OrderByDescending(x => x.QuotationDate).ThenBy(x => x.QuotationId)
            .Skip((page - 1) * PageSize).Take(PageSize);
        var items = await QuotationTrackingReader.Project(rows).ToListAsync(cancellationToken);
        return new(items, count, page, PageSize);
    }
}
