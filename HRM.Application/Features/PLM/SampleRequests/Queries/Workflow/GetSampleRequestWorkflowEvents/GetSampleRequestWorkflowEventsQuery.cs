using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestWorkflowEvents;

public sealed record GetSampleRequestWorkflowEventsQuery(Guid SampleRequestId, int PageNumber = 1, int PageSize = 20)
    : IRequest<PagedResult<SampleRequestWorkflowEventDto>?>;
