using HRM.Application.Commons.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.Tracking;

internal sealed class ListProductionTrackingQueryHandler(ProductionTrackingReader reader)
    : IRequestHandler<ListProductionTrackingQuery, PagedResult<ProductionTrackingDto>?>
{
    public async Task<PagedResult<ProductionTrackingDto>?> Handle(ListProductionTrackingQuery request, CancellationToken ct)
    {
        const int pageSize = 25;
        var query = reader.Read();
        if (query is null) return null;

        var keyword = request.Keyword?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(order => order.ExternalId.ToLower().Contains(keyword) ||
                (order.ProductExternalIdSnapshot != null && order.ProductExternalIdSnapshot.ToLower().Contains(keyword)) ||
                (order.ProductNameSnapshot != null && order.ProductNameSnapshot.ToLower().Contains(keyword)));

        var total = await query.CountAsync(ct);
        var page = Math.Clamp(request.PageNumber, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var rows = query.OrderByDescending(order => order.CreatedDate).ThenBy(order => order.MfgProductionOrderId)
            .Skip((page - 1) * pageSize).Take(pageSize);
        return new(await ProductionTrackingReader.Project(rows).ToListAsync(ct), total, page, pageSize);
    }
}
