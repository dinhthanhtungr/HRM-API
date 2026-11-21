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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

internal sealed class GetSampleRequestDailyWorkQueryHandler
    : IRequestHandler<GetSampleRequestDailyWorkQuery, PagedResult<SampleRequestSampleTrialReportDto>>
{
    private readonly ISender _sender;
    private readonly ICRMReadDbContext _crmDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentUserPermissionService _permissions;
    private readonly ILogger<GetSampleRequestDailyWorkQueryHandler> _logger;

    public GetSampleRequestDailyWorkQueryHandler(
        ISender sender,
        ICRMReadDbContext crmDbContext,
        ICurrentUser currentUser, ICurrentUserPermissionService permissions,
        ILogger<GetSampleRequestDailyWorkQueryHandler>? logger = null)
    {
        _sender = sender;
        _crmDbContext = crmDbContext;
        _currentUser = currentUser;
        _permissions = permissions;
        _logger = logger ?? NullLogger<GetSampleRequestDailyWorkQueryHandler>.Instance;
    }

    public async Task<PagedResult<SampleRequestSampleTrialReportDto>> Handle(
        GetSampleRequestDailyWorkQuery request,
        CancellationToken cancellationToken)
    {
        using var timings = new SampleWorkQueryTimings(_logger, request.Audience.HasValue || request.WorkflowDetail || request.ReadSelectedReportPage, "report");
        if (request.Audience is { } audience && !SampleRequestWorkQueueAccess.CanRead(_currentUser, audience))
            throw new UnauthorizedAccessException("The current user cannot access this sample work queue.");
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.Today);
        var workSort = string.Equals(request.NormalizedSortBy, "CustomerCode", StringComparison.OrdinalIgnoreCase)
            ? SampleRequestWorkSort.CustomerCode : string.Equals(request.NormalizedSortBy, "Newest", StringComparison.OrdinalIgnoreCase)
                ? SampleRequestWorkSort.Newest : SampleRequestWorkSort.WorkPriority;
        // Dùng cùng projection, company/customer scope và field visibility với sample-trials.
        var sampleTrialsQuery = new GetSampleRequestSampleTrialsQuery
        {
            DailyWorkDate = date,
            WorkflowDetail = request.WorkflowDetail,
            ReadSelectedReportPage = request.ReadSelectedReportPage,
            UseOperationalScope = request.UseOperationalScope,
            DailyWorkView = request.Audience.HasValue ? SampleRequestDailyView.All : request.View,
            WorkAudience = request.Audience,
            GroupByCustomer = request.GroupByCustomer && workSort != SampleRequestWorkSort.Newest,
            WorkSort = request.WorkflowDetail || request.ReadSelectedReportPage ? null : workSort,
            LabTeam = request.LabTeam ?? (request.Audience == SampleRequestWorkAudience.LabAdmin &&
                request.Bucket == SampleRequestWorkBucket.LabProgress ? Services.SampleLabTeamFilter.All : null),
            WorkBucket = request.Bucket,
            PriorityTasks = request.Bucket is SampleRequestWorkBucket.MyActionRequired or SampleRequestWorkBucket.ActionRequired
                ? SampleWorkTaskPriority.Normalize(request.PriorityTasks) : [],
            CustomerId = request.CustomerId,
            SaleEmployeeIds = request.SaleEmployeeIds,
            SaleGroupIds = request.SaleGroupIds,
            SaleEmployeeId = !request.Audience.HasValue && request.View is SampleRequestDailyView.SalePending or SampleRequestDailyView.SaleToday
                ? _currentUser.EmployeeId ?? Guid.Empty : request.SaleEmployeeId,
            SampleRequestId = request.SampleRequestId,
            SampleRequestIds = request.SampleRequestIds,
            Keyword = request.Keyword,
            SearchSelections = request.SearchSelections,
            SuggestionKeyword = request.SuggestionKeyword,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Currency = request.Currency
        };
        var report = await _sender.Send(sampleTrialsQuery, cancellationToken);
        if (report.SearchSuggestions is not null) return report;
        timings.Mark("sample-query");
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

        timings.Mark("creator-leaders");
        var productIds = report.Items
            .Where(x => (SampleRequestDailyWorkRules.IsQuotation(x.InfoType) || x.RequiresCustomerPriceConfirmation) && SampleRequestPriceConfirmationBasis.ResolveFormulaId(x).HasValue)
            .Select(x => x.ProductId).Distinct().ToArray();
        var approvedFormulaByProduct = new Dictionary<Guid, Guid?>();
        var approvedMilestones = new Dictionary<Guid, (DateTime? At, string? By)>();
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
                .Select(x => new { x.ProductId, x.SourceFormulaId, x.ApprovedAt,
                    By = x.ApprovedByNavigation != null && x.ApprovedByNavigation.CompanyId == companyId ? x.ApprovedByNavigation.FullName : null })
                .ToListAsync(cancellationToken);
            approvedFormulaByProduct = versions
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.First().SourceFormulaId);
            approvedMilestones = versions.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => (x.First().ApprovedAt, x.First().By));
        }

        timings.Mark("approved-prices");
        var canHandleLab = _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SampleRequestLabProgressEditors);
        var canApprovePrice = _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaPricingEditors);
        var canHandleSale = _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow);
        foreach (var item in report.Items)
        {
            item.FormulaCreatorLeaderNames ??= [];
            item.HasApprovedStandardPrice = approvedFormulaByProduct.TryGetValue(item.ProductId, out var approvedFormulaId) &&
                SampleRequestPriceConfirmationBasis.HasApprovedPriceSource(approvedFormulaId);
            if (item.HasApprovedStandardPrice && approvedMilestones.TryGetValue(item.ProductId, out var milestone))
            {
                item.ApprovedStandardPriceMilestoneAt = milestone.At;
                item.ApprovedStandardPriceMilestoneBy = milestone.By;
            }
            SampleRequestDailyWorkRules.Apply(
                item, date,
                item.HasApprovedStandardPrice,
                canHandleLab, canApprovePrice, canHandleSale, _currentUser.EmployeeId, !request.Audience.HasValue);
            Services.SampleWorkAssignment.Apply(item, _currentUser, _permissions, request.Audience);
        }

        if (!request.SkipUnread && report.Items.Any())
        {
            foreach (var ids in report.Items.Select(x => x.SampleRequestId).Distinct().Chunk(100))
            {
                var unread = await _sender.Send(new HRM.Application.Features.InternalMail.Queries.SampleWorkUnread.GetSampleWorkUnreadQuery(ids), cancellationToken);
                foreach (var item in report.Items.Where(x => ids.Contains(x.SampleRequestId)))
                    item.UnreadMessageCount = unread != null && unread.TryGetValue(item.SampleRequestId, out var count) ? count : 0;
            }
        }
        return report;
    }
}
