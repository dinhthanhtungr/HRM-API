using HRM.Application.Commons.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.Tracking;

internal sealed class ListSaleOrderTrackingQueryHandler(SaleOrderTrackingReader reader)
    : IRequestHandler<ListSaleOrderTrackingQuery, PagedResult<SaleOrderTrackingDto>?>
{
    private const int PageSize = 25;

    public async Task<PagedResult<SaleOrderTrackingDto>?> Handle(ListSaleOrderTrackingQuery request, CancellationToken cancellationToken)
    {
        var query = reader.Read();
        if (query is null) return null;

        var count = await query.CountAsync(cancellationToken);
        var page = Math.Clamp(request.PageNumber, 1, Math.Max(1, (int)Math.Ceiling(count / (double)PageSize)));
        var rows = query.OrderByDescending(order => order.CreateDate).ThenBy(order => order.MerchandiseOrderId)
            .Skip((page - 1) * PageSize).Take(PageSize);
        var items = await SaleOrderTrackingReader.Project(rows).ToListAsync(cancellationToken);
        return new(items, count, page, PageSize);
    }
}
