using HRM.Application.Features.PLM.SampleRequests.Dtos.Detail;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail;

public sealed class GetSampleRequestDetailQuery : IRequest<SampleRequestDetailDto?>
{
    public Guid SampleRequestId { get; set; }
}
