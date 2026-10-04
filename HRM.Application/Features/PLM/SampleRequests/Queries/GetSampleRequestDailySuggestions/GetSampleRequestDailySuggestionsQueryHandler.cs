using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.Shared.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailySuggestions;

internal sealed class GetSampleRequestDailySuggestionsQueryHandler
    : IRequestHandler<GetSampleRequestDailySuggestionsQuery, SampleRequestDailySuggestionsDto>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetSampleRequestDailySuggestionsQueryHandler(
        IPLMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<SampleRequestDailySuggestionsDto> Handle(
        GetSampleRequestDailySuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        var tokens = request.Q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return new SampleRequestDailySuggestionsDto();

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visible = _visibilityService.ApplySampleRequestVisibility(
                _dbContext.SampleRequests.AsNoTracking().Where(x => x.IsActive),
                _dbContext.Customers.AsNoTracking(), scope)
            .Where(x => x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);
        if (request.CustomerId.HasValue)
            visible = visible.Where(x => x.CustomerId == request.CustomerId.Value);

        var activeTrials = _dbContext.SampleRequestSampleTrials.AsNoTracking().Where(x => x.IsActive);
        var latestTrialNumbers = activeTrials
            .GroupBy(x => x.SampleRequestId)
            .Select(x => new { SampleRequestId = x.Key, TrialNo = x.Max(t => t.TrialNo) });
        var latestTrials =
            from trial in activeTrials
            join latest in latestTrialNumbers
                on new { trial.SampleRequestId, trial.TrialNo }
                equals new { latest.SampleRequestId, latest.TrialNo }
            select trial;
        var rows =
            from sampleRequest in visible
            join trial in latestTrials on sampleRequest.SampleRequestId equals trial.SampleRequestId into trials
            from trial in trials.DefaultIfEmpty()
            select new SampleRequestSampleTrialReportRow { SampleRequest = sampleRequest, Trial = trial };

        rows = SampleRequestSampleTrialReportQueryRules.ApplyReportType(rows,
            new GetSampleRequestSampleTrialsQuery
            {
                DailyWorkDate = request.Date ?? DateOnly.FromDateTime(DateTime.Today),
                DailyWorkView = request.View
            });

        var saleRows = rows;
        var customerRows = rows;
        var requestRows = rows;
        foreach (var token in tokens)
        {
            var pattern = PostgresSearchPattern.ContainsLiteral(token);
            saleRows = saleRows.Where(x => EF.Functions.ILike(
                x.SampleRequest.ManagerByNavigation.FullName, pattern, PostgresSearchPattern.EscapeCharacter));
            customerRows = customerRows.Where(x => EF.Functions.ILike(
                x.SampleRequest.Customer.CustomerName, pattern, PostgresSearchPattern.EscapeCharacter));
            requestRows = requestRows.Where(x => EF.Functions.ILike(
                x.SampleRequest.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter));
        }

        var sales = await saleRows.Select(x => new
            {
                Id = x.SampleRequest.ManagerBy,
                Label = x.SampleRequest.ManagerByNavigation.FullName
            }).Distinct().OrderBy(x => x.Label).Take(8).ToListAsync(cancellationToken);
        var customers = await customerRows.Select(x => new
            {
                Id = x.SampleRequest.CustomerId,
                Label = x.SampleRequest.Customer.CustomerName
            }).Distinct().OrderBy(x => x.Label).Take(8).ToListAsync(cancellationToken);
        var requests = await requestRows.Select(x => new
            {
                Id = x.SampleRequest.SampleRequestId,
                Label = x.SampleRequest.ExternalId
            }).Distinct().OrderBy(x => x.Label).Take(8).ToListAsync(cancellationToken);

        var items = sales.Take(3).Select(x => new SampleRequestDailySuggestionDto
            {
                Type = "sale", Id = x.Id, Label = x.Label, Description = "Sale phụ trách"
            })
            .Concat(customers.Take(3).Select(x => new SampleRequestDailySuggestionDto
            {
                Type = "customer", Id = x.Id, Label = x.Label, Description = "Khách hàng"
            }))
            .Concat(requests.Take(2).Select(x => new SampleRequestDailySuggestionDto
            {
                Type = "sampleRequest", Id = x.Id, Label = x.Label, Description = "Mã yêu cầu"
            }))
            .Take(8).ToArray();

        return new SampleRequestDailySuggestionsDto { Items = items };
    }
}
