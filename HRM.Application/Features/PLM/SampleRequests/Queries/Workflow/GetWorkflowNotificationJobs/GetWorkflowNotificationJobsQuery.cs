using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetWorkflowNotificationJobs;

public sealed record GetWorkflowNotificationJobsQuery(string? SampleRequestCode = null, string Status = "Attention",
    int PageNumber = 1, int PageSize = 20) : IRequest<PagedResult<WorkflowNotificationJobDto>?>;
