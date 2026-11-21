using HRM.Application.Commons.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.Tracking;

internal sealed class ListSampleProductionTrackingQueryHandler(SampleProductionTrackingReader reader)
    : IRequestHandler<ListSampleProductionTrackingQuery, PagedResult<SampleProductionTrackingDto>?>
{
    public async Task<PagedResult<SampleProductionTrackingDto>?> Handle(ListSampleProductionTrackingQuery request, CancellationToken ct)
    {
        const int size = 25;
        var query = reader.Read();
        if (query is null) return null;

        var total = await query.CountAsync(ct);
        var page = Math.Clamp(request.PageNumber, 1, Math.Max(1, (int)Math.Ceiling(total / (double)size)));
        var items = await query.OrderByDescending(order => order.CreatedDate).ThenBy(order => order.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(items, total, page, size);
    }
}
