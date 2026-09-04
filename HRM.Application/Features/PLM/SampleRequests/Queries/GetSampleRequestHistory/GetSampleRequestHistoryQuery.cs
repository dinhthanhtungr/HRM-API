using HRM.Application.Features.PLM.SampleRequests.Dtos.History;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestHistory;

public sealed class GetSampleRequestHistoryQuery : IRequest<SampleRequestHistoryResponseDto?>
{
    public Guid SampleRequestId { get; init; }
}
