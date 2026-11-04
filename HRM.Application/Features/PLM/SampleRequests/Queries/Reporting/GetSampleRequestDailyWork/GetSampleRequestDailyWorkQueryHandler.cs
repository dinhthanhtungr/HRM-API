using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Rules;
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
    private readonly ICurrentUserPermissionService _permissions;

    public GetSampleRequestDailyWorkQueryHandler(
        ISender sender,
        ICRMReadDbContext crmDbContext,
        ICurrentUser currentUser, ICurrentUserPermissionService permissions)
    {
        _sender = sender;
        _crmDbContext = crmDbContext;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<PagedResult<SampleRequestSampleTrialReportDto>> Handle(
        GetSampleRequestDailyWorkQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Audience is { } audience && !SampleRequestWorkQueueAccess.CanRead(_currentUser, audience))
            throw new UnauthorizedAccessException("The current user cannot access this sample work queue.");
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.Today);
        // Dùng cùng projection, company/customer scope và field visibility với sample-trials.
        var sampleTrialsQuery = new GetSampleRequestSampleTrialsQuery
        {
            DailyWorkDate = date,
            UseOperationalScope = request.UseOperationalScope,
            DailyWorkView = request.Audience.HasValue ? SampleRequestDailyView.All : request.View,
            WorkAudience = request.Audience,
            LabTeam = request.LabTeam,
            WorkBucket = request.Bucket,
            CustomerId = request.CustomerId,
            SaleEmployeeId = !request.Audience.HasValue && request.View is SampleRequestDailyView.SalePending or SampleRequestDailyView.SaleToday
                ? _currentUser.EmployeeId ?? Guid.Empty : request.SaleEmployeeId,
            SampleRequestId = request.SampleRequestId,
            Keyword = request.Keyword,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Currency = request.Currency
        };
        var report = await _sender.Send(sampleTrialsQuery, cancellationToken);
        var creatorIds = report.Items.Where(x => x.FormulaCreatedByEmployeeId.HasValue)
            .Select(x => x.FormulaCreatedByEmployeeId!.Value).Distinct().ToArray();
        if (creatorIds.Length > 0 && _currentUser.CompanyId is { } creatorCompanyId)
        {
            var leaders = await SampleRequestFormulaCreatorLeaders.Build(
                _crmDbContext.MemberInGroups.AsNoTracking(), creatorCompanyId, creatorIds)
                .ToListAsync(cancellationToken);
            var byCreator = leaders.GroupBy(x => x.CreatorEmployeeId)
                .ToDictionary(x => x.Key, x => x.OrderBy(y => y.LeaderName)
                    .Select(y => y.LeaderName).Distinct().ToArray());
            foreach (var item in report.Items)
                item.FormulaCreatorLeaderNames = item.FormulaCreatedByEmployeeId is { } creatorId &&
                    byCreator.TryGetValue(creatorId, out var names) ? names : [];
        }

        var productIds = report.Items
            .Where(x => (SampleRequestDailyWorkRules.IsQuotation(x.InfoType) || x.RequiresCustomerPriceConfirmation) && SampleRequestPriceConfirmationBasis.ResolveFormulaId(x).HasValue)
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
            item.FormulaCreatorLeaderNames ??= [];
            SampleRequestDailyWorkRules.Apply(
                item, date,
                approvedFormulaByProduct.TryGetValue(item.ProductId, out var pricedFormulaId) &&
                    SampleRequestPriceConfirmationBasis.MatchesApprovedFormula(item, pricedFormulaId),
                canHandleLab, canApprovePrice, canHandleSale, _currentUser.EmployeeId, !request.Audience.HasValue);
            Services.SampleWorkAssignment.Apply(item, _currentUser, _permissions, request.Audience);
        }

        return report;
    }
}
