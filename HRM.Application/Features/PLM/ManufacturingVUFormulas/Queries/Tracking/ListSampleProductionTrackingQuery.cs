using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.Tracking;

public sealed record ListSampleProductionTrackingQuery(int PageNumber)
    : IRequest<PagedResult<SampleProductionTrackingDto>?>;
