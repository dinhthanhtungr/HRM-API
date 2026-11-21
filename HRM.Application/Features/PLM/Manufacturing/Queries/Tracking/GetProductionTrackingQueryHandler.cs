using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.Tracking;

internal sealed class GetProductionTrackingQueryHandler(ProductionTrackingReader reader)
    : IRequestHandler<GetProductionTrackingQuery, ProductionTrackingDto?>
{
    public Task<ProductionTrackingDto?> Handle(GetProductionTrackingQuery request, CancellationToken ct)
    {
        var query = reader.Read()?.Where(order => order.MfgProductionOrderId == request.Id);
        return query is null
            ? Task.FromResult<ProductionTrackingDto?>(null)
            : ProductionTrackingReader.Project(query).SingleOrDefaultAsync(ct);
    }
}
