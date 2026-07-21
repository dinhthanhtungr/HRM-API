using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;

namespace HRM.Application.Features.CRM.InteractionSummaries.Queries.GetLatestCustomerInteractionSummary;

/// <summary>
/// Đọc AI summary mới nhất sau khi kiểm tra company và customer visibility scope để tránh IDOR.
/// </summary>
internal sealed class GetLatestCustomerInteractionSummaryQueryHandler
    : IRequestHandler<GetLatestCustomerInteractionSummaryQuery, OperationResult<CustomerInteractionAiSummaryDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly CustomerCrmAccessService _accessService;

    public GetLatestCustomerInteractionSummaryQueryHandler(ICRMReadDbContext dbContext, CustomerCrmAccessService accessService)
    {
        _dbContext = dbContext;
        _accessService = accessService;
    }

    /// <summary>
    /// Trả summary có PeriodTo mới nhất, sau đó ưu tiên CreatedDate mới nhất nếu cùng kỳ.
    /// </summary>
    public async Task<OperationResult<CustomerInteractionAiSummaryDto>> Handle(
        GetLatestCustomerInteractionSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        if (!await _accessService.VisibleCustomers(scope).AnyAsync(x => x.CustomerId == request.CustomerId, cancellationToken))
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail("Customer was not found.");
        var dto = await _dbContext.CustomerInteractionAiSummaries.AsNoTracking()
            .Where(x => x.CustomerId == request.CustomerId && x.CompanyId == scope.CompanyId && x.IsActive)
            .OrderByDescending(x => x.PeriodTo).ThenByDescending(x => x.CreatedDate)
            .Select(x => new CustomerInteractionAiSummaryDto
            {
                Id = x.Id, CustomerId = x.CustomerId, CustomerCode = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName, SaleEmployeeId = x.SaleEmployeeId,
                SaleEmployeeCode = x.SaleEmployee != null ? x.SaleEmployee.ExternalId : null,
                SaleEmployeeName = x.SaleEmployee != null ? x.SaleEmployee.FullName : null,
                CompanyId = x.CompanyId, SummaryScope = x.SummaryScope, Year = x.Year, Month = x.Month,
                PeriodFrom = x.PeriodFrom, PeriodTo = x.PeriodTo, InteractionCount = x.InteractionCount,
                PreviousSummary = x.PreviousSummary, Summary = x.Summary, CustomerNeed = x.CustomerNeed,
                CurrentStage = x.CurrentStage, NextAction = x.NextAction, Risk = x.Risk, Sentiment = x.Sentiment,
                SourceModel = x.SourceModel, PromptVersion = x.PromptVersion, IsAiSuccess = x.IsAiSuccess,
                IsAiSkipped = x.IsAiSkipped, AiErrorMessage = x.AiErrorMessage, AiGeneratedDate = x.AiGeneratedDate,
                CreatedDate = x.CreatedDate, UpdatedDate = x.UpdatedDate, IsActive = x.IsActive
            }).FirstOrDefaultAsync(cancellationToken);
        return dto is null
            ? OperationResult<CustomerInteractionAiSummaryDto>.Fail("AI summary was not found.")
            : OperationResult<CustomerInteractionAiSummaryDto>.Ok(dto);
    }
}
