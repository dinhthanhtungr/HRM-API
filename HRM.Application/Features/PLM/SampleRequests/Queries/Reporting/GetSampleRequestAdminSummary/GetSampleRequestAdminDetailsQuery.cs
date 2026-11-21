using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

public sealed class GetSampleRequestAdminDetailsQuery : PaginationQuery, IRequest<PagedResult<SampleRequestSampleTrialReportDto>>
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? CreatorEmployeeId { get; init; }
    public SampleRequestAdminMetric Metric { get; init; } = SampleRequestAdminMetric.Open;
}
