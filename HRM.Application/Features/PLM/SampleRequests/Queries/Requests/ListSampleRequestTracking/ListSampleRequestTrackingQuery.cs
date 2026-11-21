using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestTracking;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.ListSampleRequestTracking;

public sealed record ListSampleRequestTrackingQuery(string? Keyword, int PageNumber)
    : IRequest<PagedResult<SampleRequestTrackingDto>?>;
