using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using MediatR;
using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleActivityDetails;

public sealed class GetSampleActivityDetailsQuery : PaginationQuery, IRequest<PagedResult<SampleActivityDetailDto>>
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public string? Kind { get; init; }
    public string? Outcome { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? Team { get; init; }
    public bool HasValidFilters() => Kind is null or "quotation" or "chip" or "sample" or "other" &&
        Outcome is null or "accepted" or "rejected" or "waiting" or "cancelled" or "labDeclined" or "unknown" or "optional" or "responses" &&
        Team is null or SampleRequestLabTeamRules.Rd or SampleRequestLabTeamRules.Color or "multiple" or "unassigned";
}
