using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.Tracking;

internal sealed class GetSampleProductionTrackingQueryHandler(SampleProductionTrackingReader reader)
    : IRequestHandler<GetSampleProductionTrackingQuery, SampleProductionTrackingDto?>
{
    public Task<SampleProductionTrackingDto?> Handle(GetSampleProductionTrackingQuery request, CancellationToken ct)
    {
        var query = reader.Read();
        return query is null ? Task.FromResult<SampleProductionTrackingDto?>(null) :
            query.SingleOrDefaultAsync(order => order.Id == request.Id, ct);
    }
}
