using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal sealed class GetSampleRequestDailyWorkQueryHandler
    : IRequestHandler<GetSampleRequestDailyWorkQuery, PagedResult<SampleRequestSampleTrialReportDto>>
{
    private readonly ISender _sender;
    private readonly ICRMReadDbContext _crmDbContext;
    private readonly ICurrentUser _currentUser;

    public GetSampleRequestDailyWorkQueryHandler(
        ISender sender,
        ICRMReadDbContext crmDbContext,
        ICurrentUser currentUser)
    {
        _sender = sender;
        _crmDbContext = crmDbContext;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<SampleRequestSampleTrialReportDto>> Handle(
        GetSampleRequestDailyWorkQuery request,
        CancellationToken cancellationToken)
    {
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.Today);
        // Dùng cùng projection, company/customer scope và field visibility với sample-trials.
        var sampleTrialsQuery = new GetSampleRequestSampleTrialsQuery
        {
            DailyWorkDate = date,
            DailyWorkView = request.View,
            CustomerId = request.CustomerId,
            SaleEmployeeId = request.SaleEmployeeId,
            SampleRequestId = request.SampleRequestId,
            Keyword = request.Keyword,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Currency = request.Currency
        };
        var report = await _sender.Send(sampleTrialsQuery, cancellationToken);

        var productIds = report.Items
            .Where(x => SampleRequestDailyWorkRules.IsQuotation(x.InfoType) && x.FormulaId.HasValue)
            .Select(x => x.ProductId).Distinct().ToArray();
        var approvedFormulaByProduct = new Dictionary<Guid, Guid?>();
        if (productIds.Length > 0 && _currentUser.CompanyId is { } companyId)
        {
            var versions = await _crmDbContext.ProductPricingVersions.AsNoTracking()
                .Where(x => x.IsActive && x.CompanyId == companyId &&
                    x.Status == ProductPricingStatus.Approved &&
                    x.StandardSellingPrice > 0m &&
                    x.Currency == sampleTrialsQuery.NormalizedCurrency &&
                    productIds.Contains(x.ProductId))
                .OrderByDescending(x => x.Version)
                .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
                .Select(x => new { x.ProductId, x.SourceFormulaId })
                .ToListAsync(cancellationToken);
            approvedFormulaByProduct = versions
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.First().SourceFormulaId);
        }

        var canHandleLab = _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SampleRequestLabProgressEditors);
        var canApprovePrice = _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaPricingEditors);
        var canHandleSale = _currentUser.IsInAnyRole(ApplicationRoleSets.Modules.Sales);
        foreach (var item in report.Items)
        {
            SampleRequestDailyWorkRules.Apply(
                item, date,
                item.FormulaId.HasValue &&
                    approvedFormulaByProduct.TryGetValue(item.ProductId, out var pricedFormulaId) &&
                    pricedFormulaId == item.FormulaId.Value,
                canHandleLab, canApprovePrice, canHandleSale, _currentUser.EmployeeId);
        }

        return report;
    }
}
