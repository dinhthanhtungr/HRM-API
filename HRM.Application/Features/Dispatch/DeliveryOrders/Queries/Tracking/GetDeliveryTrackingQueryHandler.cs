using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.Tracking;

internal sealed class GetDeliveryTrackingQueryHandler(DeliveryTrackingReader reader)
    : IRequestHandler<GetDeliveryTrackingQuery, DeliveryTrackingDetailDto?>
{
    public async Task<DeliveryTrackingDetailDto?> Handle(GetDeliveryTrackingQuery request, CancellationToken ct)
    {
        var source = reader.Read();
        if (source is null) return null;

        var query = source.Where(order => order.Id == request.Id);
        var delivery = await DeliveryTrackingReader.Project(query).SingleOrDefaultAsync(ct);
        if (delivery is null) return null;

        var lines = await DeliveryTrackingReader.ProjectLines(query).ToListAsync(ct);
        return new(delivery, lines);
    }
}
