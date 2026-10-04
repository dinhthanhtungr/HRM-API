using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestPeriodReport;

internal sealed class GetSampleRequestPeriodReportQueryHandler
    : IRequestHandler<GetSampleRequestPeriodReportQuery, SampleRequestPeriodReportDto>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetSampleRequestPeriodReportQueryHandler(
        IPLMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<SampleRequestPeriodReportDto> Handle(
        GetSampleRequestPeriodReportQuery request,
        CancellationToken cancellationToken)
    {
        var start = request.FromDate.ToDateTime(TimeOnly.MinValue);
        var end = request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visible = _visibilityService.ApplySampleRequestVisibility(
                _dbContext.SampleRequests.AsNoTracking().Where(x => x.IsActive),
                _dbContext.Customers.AsNoTracking(), scope)
            .Where(x => x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);

        var outcomes = await (
            from trial in _dbContext.SampleRequestSampleTrials.AsNoTracking()
            join sampleRequest in visible on trial.SampleRequestId equals sampleRequest.SampleRequestId
            where trial.IsActive && trial.CustomerReplyDate >= start && trial.CustomerReplyDate < end &&
                  (trial.Status == SampleTrialStatus.Approved ||
                   trial.Status == SampleTrialStatus.Failed ||
                   trial.Status == SampleTrialStatus.Cancelled)
            group trial by trial.Status into groupByStatus
            select new { Status = groupByStatus.Key, Count = groupByStatus.Count() }
        ).ToListAsync(cancellationToken);

        // IsDelayed là cờ hiện tại. Chỉ đếm yêu cầu có hạn liên quan trong khoảng chọn;
        // không suy diễn lịch sử đã bật/tắt delay từ cờ này.
        var currentDelayed = await visible.CountAsync(x => x.IsDelayed == true &&
            (((EF.Functions.ILike(x.InfoType ?? string.Empty, "Quotation") ||
               EF.Functions.ILike(x.InfoType ?? string.Empty, "Báo giá")) &&
              x.ExpectedPriceQuoteDate >= start && x.ExpectedPriceQuoteDate < end) ||
             (!EF.Functions.ILike(x.InfoType ?? string.Empty, "Quotation") &&
              !EF.Functions.ILike(x.InfoType ?? string.Empty, "Báo giá") &&
              x.ExpectedDeliveryDate >= start && x.ExpectedDeliveryDate < end)),
            cancellationToken);

        return new SampleRequestPeriodReportDto
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            ApprovedTrialCount = outcomes.FirstOrDefault(x => x.Status == SampleTrialStatus.Approved)?.Count ?? 0,
            FailedTrialCount = outcomes.FirstOrDefault(x => x.Status == SampleTrialStatus.Failed)?.Count ?? 0,
            CancelledTrialCount = outcomes.FirstOrDefault(x => x.Status == SampleTrialStatus.Cancelled)?.Count ?? 0,
            CurrentlyDelayedSampleRequestCount = currentDelayed
        };
    }
}
