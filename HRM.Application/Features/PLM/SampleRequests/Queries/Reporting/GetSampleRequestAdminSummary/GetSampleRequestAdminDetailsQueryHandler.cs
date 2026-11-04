using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;

internal sealed class GetSampleRequestAdminDetailsQueryHandler(ICurrentUser user, ISender sender)
    : IRequestHandler<GetSampleRequestAdminDetailsQuery, PagedResult<SampleRequestSampleTrialReportDto>>
{
    public Task<PagedResult<SampleRequestSampleTrialReportDto>> Handle(GetSampleRequestAdminDetailsQuery request, CancellationToken ct)
    {
        if (!SampleRequestReportAccess.CanRead(user))
            throw new UnauthorizedAccessException("Sample reporting requires an authenticated employee and company.");
        if (!SampleRequestAdminReportQuery.IsValidPeriod(request.FromDate, request.ToDate) || !Enum.IsDefined(request.Metric))
            throw new ArgumentException("Invalid report period or metric.");
        return sender.Send(new GetSampleRequestSampleTrialsQuery
        {
            DailyWorkDate = request.ToDate, DailyWorkView = SampleRequestDailyView.All,
            AdminPeriodFrom = request.FromDate, AdminPeriodTo = request.ToDate,
            AdminMetric = request.Metric, AdminBranchId = request.BranchId,
            Keyword = request.Keyword, PageNumber = request.PageNumber, PageSize = request.PageSize
        }, ct);
    }
}
