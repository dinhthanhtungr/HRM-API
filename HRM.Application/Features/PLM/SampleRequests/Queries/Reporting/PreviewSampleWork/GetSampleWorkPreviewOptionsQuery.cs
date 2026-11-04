using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.PreviewSampleWork;

public sealed record GetSampleWorkPreviewOptionsQuery : IRequest<SampleWorkPreviewOptionsDto?>;
