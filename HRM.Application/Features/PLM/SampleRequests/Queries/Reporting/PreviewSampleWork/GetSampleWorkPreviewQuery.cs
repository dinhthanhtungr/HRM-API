using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Services;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.PreviewSampleWork;

public sealed class GetSampleWorkPreviewQuery : PaginationQuery, IRequest<SampleWorkPreviewDto?>
{
    public string Persona { get; init; } = "LabUser";
    public Guid EmployeeId { get; init; }
    public SampleRequestWorkBucket Bucket { get; init; } = SampleRequestWorkBucket.ActionRequired;
    public SampleLabTeamFilter? LabTeam { get; init; }
}
