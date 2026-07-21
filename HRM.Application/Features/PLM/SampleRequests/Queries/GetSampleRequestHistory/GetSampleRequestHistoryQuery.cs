using HRM.Application.Features.PLM.SampleRequests.Dtos.History;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestHistory;

public sealed class GetSampleRequestHistoryQuery : IRequest<IReadOnlyList<SampleRequestHistoryDto>?>
{
    public Guid SampleRequestId { get; init; }
}
