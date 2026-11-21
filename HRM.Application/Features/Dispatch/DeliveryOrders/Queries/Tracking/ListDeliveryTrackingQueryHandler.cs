using HRM.Application.Commons.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.Tracking;

internal sealed class ListDeliveryTrackingQueryHandler(DeliveryTrackingReader reader)
    : IRequestHandler<ListDeliveryTrackingQuery, PagedResult<DeliveryTrackingDto>?>
{
    private const int PageSize = 25;

    public async Task<PagedResult<DeliveryTrackingDto>?> Handle(ListDeliveryTrackingQuery request, CancellationToken ct)
    {
        var query = reader.Read();
        if (query is null) return null;

        var count = await query.CountAsync(ct);
        var page = Math.Clamp(request.PageNumber, 1, Math.Max(1, (int)Math.Ceiling(count / (double)PageSize)));
        var rows = query.OrderByDescending(order => order.CreatedDate).ThenBy(order => order.Id)
            .Skip((page - 1) * PageSize).Take(PageSize);
        var items = await DeliveryTrackingReader.Project(rows).ToListAsync(ct);
        return new(items, count, page, PageSize);
    }
}
