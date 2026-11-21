using HRM.Application.Commons.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.Tracking;

internal sealed class ListCustomerTrackingQueryHandler(CustomerTrackingReader reader)
    : IRequestHandler<ListCustomerTrackingQuery, PagedResult<CustomerTrackingDto>?>
{
    private const int PageSize = 25;

    public async Task<PagedResult<CustomerTrackingDto>?> Handle(ListCustomerTrackingQuery request, CancellationToken ct)
    {
        var query = reader.Read();
        if (query is null) return null;

        var keyword = request.Keyword?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(customer => customer.ExternalId.ToLower().Contains(keyword) ||
                customer.CustomerName.ToLower().Contains(keyword));

        var count = await query.CountAsync(ct);
        var page = Math.Clamp(request.PageNumber, 1, Math.Max(1, (int)Math.Ceiling(count / (double)PageSize)));
        var rows = query.OrderBy(customer => customer.CustomerName).ThenBy(customer => customer.CustomerId)
            .Skip((page - 1) * PageSize).Take(PageSize);
        return new(await reader.Project(rows).ToListAsync(ct), count, page, PageSize);
    }
}
