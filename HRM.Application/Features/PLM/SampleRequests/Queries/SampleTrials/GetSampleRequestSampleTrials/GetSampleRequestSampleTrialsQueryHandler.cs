using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

internal sealed class GetSampleRequestSampleTrialsQueryHandler
    : IRequestHandler<GetSampleRequestSampleTrialsQuery, PagedResult<SampleRequestSampleTrialReportDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICRMReadDbContext _crmDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly ICurrentUser _currentUser;
    private readonly ProductPricingSourceQueryService _pricingSourceQueryService;
    private readonly IPricingVisibilityService _pricingVisibilityService;
    private readonly ICurrentUserPermissionService _permissions;
    private readonly Services.SampleRequestLabTeamScope _labTeams;
    private readonly Services.SampleWorkPreviewSession? _preview;
    private readonly ILogger<GetSampleRequestSampleTrialsQueryHandler> _logger;

    public GetSampleRequestSampleTrialsQueryHandler(
        IPLMReadDbContext dbContext,
        ICRMReadDbContext crmDbContext,
        ICustomerVisibilityService visibilityService,
        IPLMFieldVisibilityService fieldVisibility,
        ICurrentUser currentUser,
        ProductPricingSourceQueryService pricingSourceQueryService,
        IPricingVisibilityService pricingVisibilityService,
        ICurrentUserPermissionService permissions,
        Services.SampleRequestLabTeamScope labTeams, Services.SampleWorkPreviewSession? preview = null,
        ILogger<GetSampleRequestSampleTrialsQueryHandler>? logger = null)
    {
        _dbContext = dbContext;
        _crmDbContext = crmDbContext;
        _visibilityService = visibilityService;
        _fieldVisibility = fieldVisibility;
        _currentUser = currentUser;
        _pricingSourceQueryService = pricingSourceQueryService;
        _pricingVisibilityService = pricingVisibilityService;
        _permissions = permissions;
        _labTeams = labTeams;
        _preview = preview;
        _logger = logger ?? NullLogger<GetSampleRequestSampleTrialsQueryHandler>.Instance;
    }

    public async Task<PagedResult<SampleRequestSampleTrialReportDto>> Handle(
        GetSampleRequestSampleTrialsQuery request,
        CancellationToken cancellationToken)
    {
        var workflowDetail = request.WorkflowDetail && request.SampleRequestId.HasValue &&
            !request.IncludeTrialHistory && !request.GroupByCustomer && request.WorkAudience == null &&
            request.NormalizedPageNumber == 1 && request.NormalizedPageSize == 1 && request.DailyWorkView == SampleRequestDailyView.All;
        var hasSmartSearch = request.SuggestionKeyword is not null || !string.IsNullOrWhiteSpace(request.SearchSelections);
        var metadataOnlySuggestions = SampleWorkSmartSearch.CanReadMetadataOnly(request);
        var singlePassPriority = SampleRequestDailyPriorityFactsPage.CanRead(request);
        var singlePassNewest = !hasSmartSearch && SampleRequestNewestFactsPage.CanRead(request);
        var codeCustomerPage = !hasSmartSearch && request.GroupByCustomer && request.WorkSort == SampleRequestWorkSort.CustomerCode &&
            request.WorkAudience.HasValue && request.WorkBucket == SampleRequestWorkBucket.All && !request.IncludeTrialHistory;
        var newestPage = !hasSmartSearch && !request.GroupByCustomer && request.WorkSort == SampleRequestWorkSort.Newest &&
            request.WorkAudience.HasValue && request.WorkBucket == SampleRequestWorkBucket.All && !request.IncludeTrialHistory;
        var selectedReportPage = request.ReadSelectedReportPage && request.SampleRequestIds is { Length: > 0 and <= 100 } &&
            !request.IncludeTrialHistory && !request.GroupByCustomer && request.WorkAudience == null &&
            request.NormalizedPageNumber == 1 && request.DailyWorkView == SampleRequestDailyView.All;
        var leanPayload = workflowDetail || selectedReportPage;
        var historyPayload = request.UseTrialHistoryPayload;
        var deferDecision = leanPayload || historyPayload || request.MenuCounts;
        var historyTiming = request.SampleRequestId.HasValue && request.IncludeTrialHistory &&
            System.Diagnostics.Activity.Current?.GetTagItem(SampleWorkDiagnosticTags.NamedTimingEnabled) is true;
        using var timings = new SampleWorkQueryTimings(_logger, request.WorkAudience.HasValue || leanPayload || historyTiming,
            historyTiming ? "trial_history" : "samples");
        if (request.WorkAudience is { } workAudience && !SampleRequestWorkQueueAccess.CanRead(_currentUser, workAudience))
            throw new UnauthorizedAccessException("The current user cannot access this sample work queue.");
        var scope = await _visibilityService.BuildReadScopeAsync(cancellationToken);
        if (historyTiming) timings.Mark("read-scope");
        var activeSaleActor = !metadataOnlySuggestions && _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow) &&
            _currentUser.EmployeeId is { } actorEmployee && actorEmployee != Guid.Empty &&
            await _dbContext.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == actorEmployee &&
                x.CompanyId == scope.CompanyId && x.IsActive, cancellationToken);
        ViewerScope? writeScope = null;
        var activeLabNoteActor = !metadataOnlySuggestions && _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow) &&
            _currentUser.EmployeeId is { } labActor && labActor != Guid.Empty &&
            await _dbContext.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == labActor &&
                x.CompanyId == scope.CompanyId && x.IsActive, cancellationToken);
        if (!metadataOnlySuggestions && (activeSaleActor || _permissions.HasPermission(ApplicationPermissions.PLM.ConfirmCustomerPrice) ||
            _permissions.HasPermission(ApplicationPermissions.PLM.ReceiveLabStandard)))
            writeScope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var allowedSaleOwners = activeSaleActor && writeScope != null
            ? SampleSaleWorkflowAccess.GetAllowedOwnerIds(_currentUser, _permissions, writeScope) : [];
        if (activeSaleActor && writeScope != null && SampleSaleWorkflowAccess.HasCompanyWideAccess(_currentUser, writeScope))
            allowedSaleOwners = await _dbContext.SampleRequests.AsNoTracking().Where(x => x.CompanyId == writeScope.CompanyId && x.IsActive)
                .Select(x => x.ManagerBy).Distinct().ToArrayAsync(cancellationToken);
        if (historyTiming) timings.Mark("actor-write-scope");
        if (request.WorkAudience == SampleRequestWorkAudience.LabAdmin || request.SampleRequestId.HasValue)
            scope = await Services.SampleWorkPlanningVisibility.ForPlanningAsync(scope, _currentUser, _dbContext.Employees, cancellationToken);
        var visibleSampleRequests = _visibilityService.ApplySampleRequestVisibility(
            _dbContext.SampleRequests.AsNoTracking().Where(x => x.IsActive),
            _dbContext.Customers.AsNoTracking(),
            scope)
            .Where(x => x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId && x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);
        if (_preview?.ActorReadScope is { } actorScope)
            visibleSampleRequests = _visibilityService.ApplySampleRequestVisibility(visibleSampleRequests,
                _dbContext.Customers.AsNoTracking(), actorScope);

        if (request.WorkAudience is SampleRequestWorkAudience.Lab or SampleRequestWorkAudience.LabAdmin)
            visibleSampleRequests = await _labTeams.ApplyAsync(visibleSampleRequests, request.LabTeam, cancellationToken);
        if (historyTiming) timings.Mark("planning-scope");

        if (request.SampleRequestId.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(
                x => x.SampleRequestId == request.SampleRequestId.Value);
        }

        if (request.SampleRequestIds is { Length: > 0 } snapshotIds)
            visibleSampleRequests = visibleSampleRequests.Where(x => snapshotIds.Contains(x.SampleRequestId));

        // Tất cả mẫu của Sale/Lab không kéo lịch sử trước mốc triển khai.
        if (request.MenuCounts)
            visibleSampleRequests = SampleRequestWorkScope.ApplyMenu(visibleSampleRequests, _dbContext.SampleRequestWorkflowEvents);
        if (!request.MenuCounts && request.WorkAudience.HasValue && request.WorkBucket == SampleRequestWorkBucket.All)
            visibleSampleRequests = visibleSampleRequests.Where(x => (x.CreatedDate >= Services.Workflow.SampleFlowRules.StartsAt || x.WorkflowEvents.Any(e => e.CompanyId == x.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)));

        // Các màn tra cứu khác giữ phạm vi hiện có; company/customer/ownership scope không đổi.
        if (!request.MenuCounts && (request.UseOperationalScope || (request.WorkAudience.HasValue && request.WorkBucket != SampleRequestWorkBucket.All) ||
            (!request.WorkAudience.HasValue && request.DailyWorkDate.HasValue && request.DailyWorkView != SampleRequestDailyView.All)))
            visibleSampleRequests = SampleRequestWorkScope.Apply(visibleSampleRequests, _dbContext.SampleRequestWorkflowEvents);

        if (request.AdminMetric is { } adminMetric && request.AdminPeriodFrom is { } adminFrom && request.AdminPeriodTo is { } adminTo)
        {
            if (!HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestReportAccess.CanRead(_currentUser))
                throw new UnauthorizedAccessException("Sample reporting requires an authenticated employee and company.");
            if (!HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminReportQuery.IsValidPeriod(adminFrom, adminTo) || !Enum.IsDefined(adminMetric))
                throw new ArgumentException("Invalid admin report period or metric.");
            var adminRows = HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminReportQuery
                .Build(visibleSampleRequests, _dbContext, _crmDbContext.ProductPricingVersions.AsNoTracking());
            var matches = HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminReportQuery
                .Filter(adminRows, adminMetric, adminFrom.ToDateTime(TimeOnly.MinValue), adminTo.AddDays(1).ToDateTime(TimeOnly.MinValue));
            if (request.AdminBranchId is { } branchId) matches = matches.Where(x => x.BranchId == branchId);
            visibleSampleRequests = visibleSampleRequests.Where(x => matches.Any(row => row.SampleRequestId == x.SampleRequestId));
        }

        if (request.CustomerId.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(
                x => x.CustomerId == request.CustomerId.Value);
        }

        // Work audience changes the work filter, not the shared request visibility scope.
        var mySaleWork = request.WorkAudience == SampleRequestWorkAudience.Sale &&
            request.WorkBucket == SampleRequestWorkBucket.MyActionRequired;
        if (mySaleWork)
        {
            var writable = writeScope == null ? _dbContext.SampleRequests.Where(_ => false) :
                _visibilityService.ApplySampleRequestVisibility(_dbContext.SampleRequests.AsNoTracking(),
                    _dbContext.Customers.AsNoTracking(), writeScope)
                .Where(x => x.CompanyId == scope.CompanyId && x.IsActive);
            visibleSampleRequests = SampleRequestWorkQueueQuery.ApplyMySaleOwnership(
                visibleSampleRequests, writable, allowedSaleOwners, _currentUser.EmployeeId);
        }
        var saleEmployeeId = request.SaleEmployeeId;
        visibleSampleRequests = SampleWorkSaleFilter.Apply(visibleSampleRequests,
            _crmDbContext.MemberInGroups, scope.CompanyId, request.SaleGroupIds, request.SaleEmployeeIds);
        if (saleEmployeeId.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(
                x => x.ManagerBy == saleEmployeeId.Value);
        }

        var sampleRequestCreatedRange =
            SampleRequestSampleTrialReportRules.ResolveCreatedRange(
                request.SampleRequestCreatedToDate,
                request.IncludePreviousUnfinished);
        if (sampleRequestCreatedRange.FromInclusive.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(
                x => x.CreatedDate >= sampleRequestCreatedRange.FromInclusive.Value);
        }

        var sampleRequestCreatedToExclusive = sampleRequestCreatedRange.ToExclusive;
        if (sampleRequestCreatedToExclusive.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(
                x => x.CreatedDate < sampleRequestCreatedToExclusive.Value);
        }

        var activeTrials = _dbContext.SampleRequestSampleTrials
            .AsNoTracking()
            .Where(x => x.IsActive);
        if (request.SampleRequestId.HasValue)
            activeTrials = activeTrials.Where(x => x.SampleRequestId == request.SampleRequestId);
        else if (request.SampleRequestIds is { Length: > 0 } trialScopeIds)
            activeTrials = activeTrials.Where(x => trialScopeIds.Contains(x.SampleRequestId));
        else if (request.WorkAudience.HasValue)
            activeTrials = activeTrials.Where(t => visibleSampleRequests.Any(s => s.SampleRequestId == t.SampleRequestId));

        var trialCounts =
            from trial in activeTrials
            group trial by trial.SampleRequestId into trialGroup
            select new
            {
                SampleRequestId = trialGroup.Key,
                Count = trialGroup.Count(),
                LatestTrialNo = trialGroup.Max(x => x.TrialNo)
            };

        var chronologicalLatestTrials =
            from trial in activeTrials
            join trialCount in trialCounts
                on new { trial.SampleRequestId, trial.TrialNo }
                equals new { trialCount.SampleRequestId, TrialNo = trialCount.LatestTrialNo }
            select trial;

        var latestTrials = DeliveryOptions.SampleDeliveryRules.Representatives(chronologicalLatestTrials, activeTrials, _dbContext.SampleRequestWorkflowEvents);

        var reportTrials = request.IncludeTrialHistory ||
            (request.DailyWorkDate == null && request.ReportType == SampleTrialReportType.WaitingCustomerFeedback)
            ? activeTrials : latestTrials;
        if (!request.IncludeTrialHistory && request.ReportType == SampleTrialReportType.WaitingCustomerFeedback)
        {
            var reportOptionalChipIds = Services.Workflow.ChipThenSampleRules.OptionalChips(_dbContext.SampleRequestSampleTrials).Select(x => x.SampleRequestSampleTrialId);
            var plannedChipIds = Services.Workflow.ChipThenSampleRules.PlannedChips(_dbContext.SampleRequestSampleTrials, _dbContext.SampleRequestWorkflowEvents).Select(x => x.SampleRequestSampleTrialId);
            reportTrials = reportTrials.Where(x => !reportOptionalChipIds.Contains(x.SampleRequestSampleTrialId) && !plannedChipIds.Contains(x.SampleRequestSampleTrialId));
        }

        var query =
            from sampleRequest in visibleSampleRequests
            join trialCount in trialCounts
                on sampleRequest.SampleRequestId equals trialCount.SampleRequestId into trialCountGroup
            from trialCount in trialCountGroup.DefaultIfEmpty()
            join trial in reportTrials
                on sampleRequest.SampleRequestId equals trial.SampleRequestId into trialGroup
            from trial in trialGroup.DefaultIfEmpty()
            select new SampleRequestSampleTrialReportRow
            {
                SampleRequest = sampleRequest,
                Trial = trial,
                TrialCount = trialCount == null ? null : trialCount.Count
            };

        var rawPayloadRows = query;
        if (historyPayload)
        {
            // Avoid joining an aggregate count into the history count/page query.
            query = SampleRequestSampleTrialReportQueryRules.HistoryRows(visibleSampleRequests, activeTrials);
            rawPayloadRows = query;
        }
        query = SampleRequestLabWorkQuery.Enrich(query, _dbContext);
        // Một TP không cần lặp biểu thức quyết định rất lớn trong SQL payload.
        // AttachAsync sẽ tính cùng canonical decision sau khi đọc facts có scope.
        if (!metadataOnlySuggestions && !deferDecision && !singlePassPriority && !singlePassNewest && !codeCustomerPage && !newestPage)
            query = Services.SampleWorkDecisionQuery.Enrich(query, _dbContext, _crmDbContext, request.NormalizedCurrency);

        query = SampleRequestSampleTrialReportQueryRules.ApplyReportType(query, request);
        if (singlePassPriority && request.WorkBucket is SampleRequestWorkBucket.Completed or
            SampleRequestWorkBucket.Cancelled or SampleRequestWorkBucket.LabDeclined or SampleRequestWorkBucket.FeedbackReceived)
            query = SampleRequestWorkQueueQuery.Apply(query, request, _crmDbContext);
        else if ((singlePassPriority || singlePassNewest) && request.WorkBucket != SampleRequestWorkBucket.All)
            query = query.Where(x => x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled" && x.SampleRequest.Status != "LabDeclined");
        else if (request.WorkAudience.HasValue)
            query = SampleRequestWorkQueueQuery.Apply(query, request, _crmDbContext);
        if (request.DailyWorkView is SampleRequestDailyView.SalePending or SampleRequestDailyView.SaleToday)
            query = SampleRequestSaleWorkQuery.RequirePublishedQuotationPrice(query, _crmDbContext, request.NormalizedCurrency);
        if (request.DailyWorkView == SampleRequestDailyView.SaleToday && request.DailyWorkDate is { } workDate)
        {
            var end = workDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
            var followUps = SampleRequestSaleFollowUpQuery.Build(_crmDbContext, scope.CompanyId, _currentUser.EmployeeId ?? Guid.Empty);
            query = query.Where(x => followUps.Any(task => task.SampleRequestId == x.SampleRequest.SampleRequestId && task.DueDate < end));
        }
        query = SampleRequestSampleTrialReportQueryRules.ApplyDateRange(query, request);

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Trial != null && x.Trial.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerReplyStatus))
        {
            var replyStatus = request.CustomerReplyStatus.Trim();
            query = query.Where(x => x.Trial != null && x.Trial.CustomerReplyStatus == replyStatus);
        }

        var smartSearch = SampleWorkSmartSearch.Build(query, _dbContext);
        query = smartSearch.ApplyRequest(query, request.NormalizedKeyword, request, out var smartKeyword);
        if (!string.IsNullOrWhiteSpace(smartKeyword))
        {
            var keyword = smartKeyword;
            if (request.DailyWorkDate.HasValue)
            {
                foreach (var token in keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var tokenPattern = PostgresSearchPattern.ContainsLiteral(token);
                    query = query.Where(x =>
                        EF.Functions.ILike(x.SampleRequest.ManagerByNavigation.FullName, tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
                        EF.Functions.ILike(x.SampleRequest.Customer.CustomerName, tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
                        EF.Functions.ILike(x.SampleRequest.Customer.ExternalId ?? string.Empty, tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
                        x.SampleRequest.Customer.CompanyId == x.SampleRequest.CompanyId && EF.Functions.ILike(x.SampleRequest.Customer.TaxNumber ?? "", tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
                        EF.Functions.ILike(x.SampleRequest.ExternalId, tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
                        EF.Functions.ILike(x.SampleRequest.Product.Name ?? string.Empty, tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
                        (x.SampleRequest.Product.IsActive && x.SampleRequest.Product.CompanyId == x.SampleRequest.CompanyId &&
                         EF.Functions.ILike(x.SampleRequest.Product.EndUser ?? string.Empty, tokenPattern, PostgresSearchPattern.EscapeCharacter)) ||
                        EF.Functions.ILike(x.SampleRequest.Product.ColourCode ?? string.Empty, tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
                        (x.Trial != null && EF.Functions.ILike(x.Trial.BatchNo ?? string.Empty, tokenPattern, PostgresSearchPattern.EscapeCharacter)) ||
                        (x.Trial != null && EF.Functions.ILike(x.Trial.LabNote ?? string.Empty, tokenPattern, PostgresSearchPattern.EscapeCharacter)) ||
                        (x.Trial != null && EF.Functions.ILike(x.Trial.CustomerReplyNote ?? string.Empty, tokenPattern, PostgresSearchPattern.EscapeCharacter)) ||
                        (x.SampleRequest.Formula != null && EF.Functions.ILike(x.SampleRequest.Formula.ExternalId, tokenPattern, PostgresSearchPattern.EscapeCharacter)) ||
                        (x.Trial != null && x.Trial.Formula != null && EF.Functions.ILike(x.Trial.Formula.ExternalId, tokenPattern, PostgresSearchPattern.EscapeCharacter)));
                }
            }
            else
            {
            query = query.Where(x =>
                EF.Functions.ILike(((x.Trial != null ? x.Trial.CustomerNameSnapshot : null) ?? x.SampleRequest.Customer.CustomerName), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                x.SampleRequest.Customer.CompanyId == x.SampleRequest.CompanyId && EF.Functions.ILike(x.SampleRequest.Customer.TaxNumber ?? "", PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(((x.Trial != null ? x.Trial.SampleRequestExternalIdSnapshot : null) ?? x.SampleRequest.ExternalId), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(((x.Trial != null ? x.Trial.ProductNameSnapshot : null) ?? x.SampleRequest.Product.Name ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                (x.SampleRequest.Product.IsActive && x.SampleRequest.Product.CompanyId == x.SampleRequest.CompanyId &&
                 EF.Functions.ILike(x.SampleRequest.Product.EndUser ?? string.Empty, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                EF.Functions.ILike(((x.Trial != null ? x.Trial.ColourCodeSnapshot : null) ?? x.SampleRequest.Product.ColourCode ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                (x.SampleRequest.Formula != null && EF.Functions.ILike(x.SampleRequest.Formula.ExternalId, $"%{keyword}%")) ||
                (x.Trial != null && x.Trial.Formula != null && EF.Functions.ILike(x.Trial.Formula.ExternalId, $"%{keyword}%")) ||
                (x.Trial != null && EF.Functions.ILike((x.Trial.BatchNo ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                (x.Trial != null && EF.Functions.ILike((x.Trial.CustomerReplyNote ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                (x.Trial != null && EF.Functions.ILike((x.Trial.LabNote ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)));
            }
        }

        if (await smartSearch.SuggestionsAsync<SampleRequestSampleTrialReportDto>(query, request, cancellationToken) is { } suggestions)
            return suggestions;
        if (request.MenuCounts && request.WorkAudience is { } menuAudience)
        {
            var writable = writeScope == null ? _dbContext.SampleRequests.Where(_ => false) :
                _visibilityService.ApplySampleRequestVisibility(_dbContext.SampleRequests.AsNoTracking(), _dbContext.Customers.AsNoTracking(), writeScope);
            var companyRequests = _dbContext.SampleRequests.AsNoTracking().Where(x => x.CompanyId == scope.CompanyId && x.IsActive);
            var allRequests = companyRequests.Where(x => x.CreatedDate >= Services.Workflow.SampleFlowRules.StartsAt ||
                x.WorkflowEvents.Any(e => e.CompanyId == x.CompanyId && e.EventType == Services.Workflow.SampleFollowUpRules.Started));
            var operational = SampleRequestWorkScope.Apply(companyRequests, _dbContext.SampleRequestWorkflowEvents);
            var facts = SampleRequestDailyPriorityFactsPage.ReadRows(query, _dbContext, _crmDbContext, request.NormalizedCurrency,
                new(menuAudience, false, activeSaleActor, false, false, allowedSaleOwners), writable, forMenu: true)
                .Select(row => new SampleRequestDailyPriorityFactsPage.MenuRow
                {
                    Row = row,
                    InAll = allRequests.Any(x => x.SampleRequestId == row.RequestId),
                    InOperational = operational.Any(x => x.SampleRequestId == row.RequestId)
                });
            var menu = await SampleRequestDailyPriorityFactsPage.CountMenuRowsAsync(facts.AsAsyncEnumerable(), menuAudience,
                _currentUser.EmployeeId, request.UseOperationalScope,
                (request.DailyWorkDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue), cancellationToken);
            return new PagedResult<SampleRequestSampleTrialReportDto>([], 0, 1, 1) { Facets = menu };
        }
        query = SampleRequestSampleTrialReportQueryRules.ApplySorting(query, request);
        var unrankedDailyRows = query;
        {
            var writableForSort = writeScope == null ? _dbContext.SampleRequests.Where(_ => false) :
                _visibilityService.ApplySampleRequestVisibility(_dbContext.SampleRequests.AsNoTracking(),
                    _dbContext.Customers.AsNoTracking(), writeScope);
            query = SampleRequestWorkSortQuery.Apply(query, request.WorkSort, leanPayload || singlePassPriority || singlePassNewest || codeCustomerPage, request.WorkAudience,
                (request.DailyWorkDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue),
                _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow), activeSaleActor,
                _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaPricingEditors),
                _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule), allowedSaleOwners, writableForSort);
        }
        timings.Mark("scope-and-shape");
        // Scoped facts are read once; canonical work-category filtering happens before
        // group count/paging, without expanding the same decision CASE in SQL WHERE.
        var totalCount = leanPayload || singlePassPriority || singlePassNewest ? 0 : request.GroupByCustomer
            ? await query.TagWith("SampleDaily.Count").Select(x => x.SampleRequest.CustomerId).Distinct().CountAsync(cancellationToken)
            : await query.TagWith("SampleDaily.Count").CountAsync(cancellationToken);
        timings.Mark("count");
        // Preferences only influence ordering of the already scoped/filtered queue.
        var pinnedRequestIds = request.WorkAudience.HasValue && request.WorkSort.HasValue &&
            _currentUser.EmployeeId is { } pinOwner && pinOwner != Guid.Empty
            ? await _dbContext.SampleRequestUserPreferences.AsNoTracking()
                .Where(x => x.CompanyId == scope.CompanyId && x.EmployeeId == pinOwner && x.IsPinned)
                .Select(x => x.SampleRequestId).ToArrayAsync(cancellationToken)
            : Array.Empty<Guid>();
        Guid[]? sortedCustomerIds = null;
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? facets = null;
        SampleRequestDailyPayloadPage? dailyPayload = null;
        IQueryable<SampleRequestSampleTrialReportRow> pageQuery;
        if (singlePassNewest)
        {
            var page = await SampleRequestNewestFactsPage.ReadAsync(unrankedDailyRows, rawPayloadRows,
                _dbContext, _crmDbContext, request.NormalizedCurrency, pinnedRequestIds,
                request.NormalizedPageNumber, request.NormalizedPageSize, cancellationToken);
            totalCount = page.TotalCount;
            dailyPayload = page.Payload;
            pageQuery = dailyPayload.Rows;
        }
        else if (singlePassPriority)
        {
            var writableForFacts = writeScope == null ? _dbContext.SampleRequests.Where(_ => false) :
                _visibilityService.ApplySampleRequestVisibility(_dbContext.SampleRequests.AsNoTracking(),
                    _dbContext.Customers.AsNoTracking(), writeScope);
            var page = await SampleRequestDailyPriorityFactsPage.ReadAsync(unrankedDailyRows, rawPayloadRows,
                _dbContext, _crmDbContext, request.NormalizedCurrency,
                new(request.WorkAudience, _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow),
                    activeSaleActor, _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaPricingEditors),
                    _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule), allowedSaleOwners,
                    request.WorkBucket is SampleRequestWorkBucket.ActionRequired or SampleRequestWorkBucket.MyActionRequired
                        ? request.WorkAudience == SampleRequestWorkAudience.Sale ? "Sale" : request.WorkAudience == SampleRequestWorkAudience.LabAdmin ? "LabAdmin" : "Lab" : null,
                    request.WorkBucket, pinnedRequestIds, request.PriorityTasks, request.GroupByCustomer),
                writableForFacts, (request.DailyWorkDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue),
                request.NormalizedPageNumber, request.NormalizedPageSize, cancellationToken);
            sortedCustomerIds = request.GroupByCustomer ? page.CustomerIds : null;
            if (page.TaskCounts is not null) facets = new Dictionary<string, IReadOnlyDictionary<string, int>> { ["workTasks"] = page.TaskCounts };
            totalCount = page.TotalCustomers;
            dailyPayload = page.Payload;
            pageQuery = dailyPayload.Rows;
        }
        else if (request.GroupByCustomer && request.WorkSort is { } customerSort)
        {
            sortedCustomerIds = customerSort == SampleRequestWorkSort.WorkPriority
                ? await SampleRequestCustomerPageQuery.SelectPriorityCustomerIdsAsync(query,
                    request.NormalizedPageNumber, request.NormalizedPageSize, cancellationToken, pinnedRequestIds)
                : await SampleRequestCustomerPageQuery.SelectCodePageCustomerIds(query,
                    request.NormalizedPageNumber, request.NormalizedPageSize, pinnedRequestIds).ToArrayAsync(cancellationToken);
            pageQuery = SampleRequestCustomerPageQuery.ExpandSelectedCustomers(query, sortedCustomerIds,
                priorityAlreadyComputed: !codeCustomerPage);
        }
        else pageQuery = request.GroupByCustomer
            ? SampleRequestCustomerPageQuery.Apply(query, request.NormalizedPageNumber, request.NormalizedPageSize)
            : (pinnedRequestIds.Length == 0 ? query : request.WorkSort == SampleRequestWorkSort.CustomerCode
                ? SampleRequestWorkSortQuery.OrderCustomerCode(query, pinnedRequestIds)
                : request.WorkSort == SampleRequestWorkSort.Newest ? SampleRequestWorkSortQuery.OrderNewest(query, pinnedRequestIds)
                : SampleRequestWorkSortQuery.OrderPriority(query, pinnedRequestIds))
                .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize);
        timings.Mark("page-selection");
        if (dailyPayload is null && request.WorkAudience.HasValue && request.WorkSort.HasValue && !request.IncludeTrialHistory)
        {
            if (codeCustomerPage)
            {
                var writableForPage = writeScope == null ? _dbContext.SampleRequests.Where(_ => false) :
                    _visibilityService.ApplySampleRequestVisibility(_dbContext.SampleRequests.AsNoTracking(),
                        _dbContext.Customers.AsNoTracking(), writeScope);
                dailyPayload = await SampleRequestDailyPayloadPage.ReadCustomerPageAsync(pageQuery, rawPayloadRows,
                    _dbContext, _crmDbContext, request.NormalizedCurrency,
                    new(request.WorkAudience, _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow),
                        activeSaleActor, _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaPricingEditors),
                        _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleWorkSchedule), allowedSaleOwners),
                    writableForPage, (request.DailyWorkDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue),
                    cancellationToken, timings.Mark);
            }
            else dailyPayload = await SampleRequestDailyPayloadPage.ReadAsync(pageQuery, rawPayloadRows,
                _dbContext, _crmDbContext, request.NormalizedCurrency, cancellationToken);
            pageQuery = dailyPayload.Rows;
        }
        Dictionary<Guid, Services.SampleWorkDecision>? historyDecisions = null;
        if (historyPayload)
        {
            // Read each bounded trial's facts once; do not inline the decision expression into SQL.
            var facts = await Services.SampleWorkDecisionQuery.ReadFacts(pageQuery, _dbContext, _crmDbContext, request.NormalizedCurrency)
                .Select(x => new { Key = x.Row.Trial != null ? x.Row.Trial.SampleRequestSampleTrialId : x.Row.SampleRequest.SampleRequestId, x.Facts })
                .ToListAsync(cancellationToken);
            historyDecisions = facts.ToDictionary(x => x.Key, x => Services.SampleWorkDecisionRules.Resolve(x.Facts));
        }
        timings.Mark("page-facts");
        var canViewTechnicalFields = _fieldVisibility.CanViewProductTechnicalInfo();
        var canUpdateCustomerFeedback = activeSaleActor;
        var canUpdateExpectedPriceQuoteDate =
            _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SampleRequestExpectedPriceQuoteDateEditors);
        var canUpdateLabProgress = _currentUser.EmployeeId.HasValue && _currentUser.EmployeeId != Guid.Empty &&
            _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SampleRequestLabProgressEditors);

        var canManageLegacyWork = _currentUser.EmployeeId is { } employeeId && employeeId != Guid.Empty &&
            _permissions.HasPermission(ApplicationPermissions.PLM.ManageLegacySampleWork);
        var canCorrectFeedback = _permissions.HasPermission(ApplicationPermissions.PLM.CorrectSampleTrialFeedback);
        var deferFormulaFields = dailyPayload != null || workflowDetail;
        var items = await pageQuery.TagWith("SampleDaily.Payload")
            .Select(x => new SampleRequestSampleTrialReportDto
            {
                WorkDecision = deferDecision || dailyPayload != null ? null : x.WorkDecision,
                LabDeclineReason = x.SampleRequest.Status == "LabDeclined"
                    ? _dbContext.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.SampleRequest.CompanyId &&
                        e.SampleRequestId == x.SampleRequest.SampleRequestId && e.EventType == SampleRequestWorkflowEvents.LabDeclined)
                        .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).Select(e => e.Note).FirstOrDefault() : null,
                CancellationNote = x.SampleRequest.Status == "Cancelled"
                    ? _dbContext.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.SampleRequest.CompanyId &&
                        e.SampleRequestId == x.SampleRequest.SampleRequestId && e.EventType == SampleRequestWorkflowEvents.RequestCancelled)
                        .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).Select(e => e.Note).FirstOrDefault()
                    : null,
                LatestLabNote = _dbContext.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.SampleRequest.CompanyId &&
                    e.SampleRequestId == x.SampleRequest.SampleRequestId && e.EventType == SampleRequestWorkflowEvents.LabNote)
                    .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                    .Select(e => new HRM.Application.Features.PLM.SampleRequests.Dtos.Reports.LabNoteDto(e.Id, e.Note ?? "", e.RecordedAt,
                        _dbContext.Employees.Where(employee => employee.CompanyId == e.CompanyId && employee.EmployeeId == e.RecordedByEmployeeId)
                            .Select(employee => employee.FullName).FirstOrDefault())).FirstOrDefault(),
                HasAvailableFormula = _dbContext.Formulas.Any(f => f.CompanyId == x.SampleRequest.CompanyId &&
                    f.ProductId == x.SampleRequest.ProductId && f.IsActive),
                NeedsLabSchedule = !deferDecision && dailyPayload == null && x.NeedsSchedule && (x.WorkDecision!.Role == "Lab" || x.WorkDecision.Role == "LabAdmin"),
                CanCorrectCustomerFeedback = canCorrectFeedback && allowedSaleOwners.Contains(x.SampleRequest.ManagerBy) &&
                    x.Trial != null && x.Trial.CustomerReplyDate.HasValue &&
                    !_dbContext.SampleRequestSampleTrials.Any(newer => newer.SampleRequestId == x.SampleRequest.SampleRequestId &&
                        newer.IsActive && newer.TrialNo > x.Trial.TrialNo),
                IsLegacyWork = x.SampleRequest.CreatedDate < SampleRequestWorkScope.StartedAt,
                IsLegacyWorkTracked = x.SampleRequest.CreatedDate < SampleRequestWorkScope.StartedAt &&
                    (x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == Services.Workflow.SampleFollowUpRules.Started) ||
                    _dbContext.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.SampleRequest.CompanyId &&
                        e.SampleRequestId == x.SampleRequest.SampleRequestId &&
                        (e.EventType == SampleRequestWorkScope.LegacyIncluded || e.EventType == SampleRequestWorkScope.LegacyExcluded))
                        .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                        .Select(e => e.EventType).FirstOrDefault() == SampleRequestWorkScope.LegacyIncluded),
                CanManageLegacyWorkTracking = x.SampleRequest.CreatedDate < SampleRequestWorkScope.StartedAt &&
                    !x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == Services.Workflow.SampleFollowUpRules.Started) && canManageLegacyWork,
                SampleRequestSampleTrialId = x.Trial != null ? x.Trial.SampleRequestSampleTrialId : null,
                SampleRequestId = x.SampleRequest.SampleRequestId,
                ProductId = x.SampleRequest.ProductId,
                ManagerSalesEmployeeId = x.SampleRequest.ManagerBy,
                InfoType = x.SampleRequest.InfoType,
                FormulaStatus = deferFormulaFields ? null : _dbContext.Formulas.Where(f => f.FormulaId == (((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt && !x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId) && f.CompanyId == x.SampleRequest.CompanyId && f.ProductId == x.SampleRequest.ProductId && f.IsActive).Select(f => f.Status).FirstOrDefault(),
                RealPriceQuoteDate = x.SampleRequest.RealPriceQuoteDate,
                FormulaId = deferFormulaFields ? null : ((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt && !x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId,
                PriceConfirmationFormulaId = deferFormulaFields ? null : ((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt && !x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId,
                FormulaExternalId = deferFormulaFields ? null : _dbContext.Formulas.Where(f => f.FormulaId == (((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt && !x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId) && f.CompanyId == x.SampleRequest.CompanyId && f.ProductId == x.SampleRequest.ProductId && f.IsActive).Select(f => f.ExternalId).FirstOrDefault(),
                TrialNo = x.Trial != null ? x.Trial.TrialNo : null,
                HasTrial = x.Trial != null,
                TrialCount = x.TrialCount ?? 0,
                HasPreviousTrials = (x.TrialCount ?? 0) > 1,
                CanCreateTrial = canViewTechnicalFields && (x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt && !x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) &&
                    x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled" && x.SampleRequest.Status != "LabDeclined",
                CanUpdateTrial = canViewTechnicalFields && x.Trial != null,
                CanUpdateExpectedPriceQuoteDate = canUpdateExpectedPriceQuoteDate && x.Trial != null,
                CanUpdateCustomerFeedback = canUpdateCustomerFeedback && allowedSaleOwners.Contains(x.SampleRequest.ManagerBy) &&
                    x.Trial != null &&
                    x.SampleRequest.Status != "Cancelled" && x.SampleRequest.Status != "LabDeclined" &&
                    (x.Trial.Status == SampleTrialStatus.SampleSent ||
                     x.Trial.Status == SampleTrialStatus.WaitingCustomerFeedback ||
                     x.Trial.Status == SampleTrialStatus.PriceQuote),

                SampleRequestStatus = x.SampleRequest.Status,
                CustomerId = x.SampleRequest.CustomerId,
                CustomerName = x.SampleRequest.Customer.CustomerName,
                CustomerExternalId = x.SampleRequest.Customer.ExternalId,
                CanRevokeCustomerPrice = true,
                ManagerSalesName = x.SampleRequest.ManagerByNavigation.FullName,

                SampleRequestExternalId = x.SampleRequest.ExternalId,
                RequestedSampleQuantity = x.SampleRequest.SampleQuantity,
                DeliveredSampleQuantityKg = x.Trial != null ? x.Trial.DeliveredSampleQuantityKg : null,

                ProductName = x.SampleRequest.Product.Name ?? string.Empty,
                CategoryName = x.SampleRequest.Product.Category != null ? x.SampleRequest.Product.Category.Name : null,
                LabCategoryCode = x.SampleRequest.Product.Category != null ? x.SampleRequest.Product.Category.ExternalId : null,
                ColourCode = x.SampleRequest.Product.ColourCode,
                BatchNo = x.Trial != null ? x.Trial.BatchNo : null,
                RequestDeliveryDate = x.SampleRequest.RequestDeliveryDate,
                LabReceivedDate = x.SampleRequest.LabReceivedDate,
                NumberDeliverySampleDate = x.SampleRequest.NumberDeliverySampleDate,
                LabFormulaConfirmedAt = deferFormulaFields ? null : x.LabFormulaConfirmedAt,
                SampleRequestUpdatedDate = x.SampleRequest.UpdatedDate,
                CanUpdateLabProgress = canUpdateLabProgress &&
                    x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled" && x.SampleRequest.Status != "LabDeclined",
                FormulaCreatedByEmployeeId = deferFormulaFields ? null : _dbContext.Formulas.Where(f => f.FormulaId == (((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt && !x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId) && f.CompanyId == x.SampleRequest.CompanyId && f.ProductId == x.SampleRequest.ProductId && f.IsActive).Select(f => f.CreatedBy).FirstOrDefault(),
                FormulaCreatedByName = deferFormulaFields ? null : _dbContext.Formulas.Where(f => f.FormulaId == (((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt && !x.SampleRequest.WorkflowEvents.Any(e => e.CompanyId == x.SampleRequest.CompanyId && e.EventType == HRM.Application.Features.PLM.SampleRequests.Services.Workflow.SampleFollowUpRules.Started)) ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId) && f.CompanyId == x.SampleRequest.CompanyId && f.ProductId == x.SampleRequest.ProductId && f.IsActive).Where(f => f.CreatedByNavigation != null).Select(f => f.CreatedByNavigation!.FullName).FirstOrDefault(),
                IsDelayed = x.SampleRequest.IsDelayed == true,
                LabReceivedByEmployeeId = x.SampleRequest.LabReceivedByEmployeeId,
                LabReceivedByName = x.SampleRequest.LabReceivedByNavigation != null
                    ? x.SampleRequest.LabReceivedByNavigation.FullName
                    : null,
                ExpectedDeliveryDate = x.SampleRequest.ExpectedDeliveryDate,
                ExpectedPriceQuoteDate = x.SampleRequest.ExpectedPriceQuoteDate,
                RequestReceivedDate = x.Trial != null ? x.Trial.RequestReceivedDate : null,
                FinishedDate = x.Trial != null ? x.Trial.FinishedDate : null,

                SentDate = x.Trial != null ? x.Trial.SentDate : null,
                SentByEmployeeId = x.Trial != null ? x.Trial.SentByEmployeeId : x.SampleRequest.SendBy,
                SentByName = x.Trial != null && x.Trial.SentByEmployeeId.HasValue
                    ? _dbContext.Employees
                        .Where(employee => employee.EmployeeId == x.Trial.SentByEmployeeId.Value)
                        .Select(employee => employee.FullName)
                        .FirstOrDefault()
                    : x.SampleRequest.SendByNavigation != null
                        ? x.SampleRequest.SendByNavigation.FullName
                        : null,

                DeliveryMethod = x.Trial != null ? x.Trial.DeliveryMethod : null,
                Status = x.Trial != null ? x.Trial.Status : null,
                CustomerReplyStatus = x.Trial != null ? x.Trial.CustomerReplyStatus : null,
                CustomerReplyDate = x.Trial != null ? x.Trial.CustomerReplyDate : null,
                CustomerReplyNote = x.Trial != null ? x.Trial.CustomerReplyNote : null,
                OrderDate = x.Trial != null ? x.Trial.OrderDate : null,
                AdditiveRate = x.SampleRequest.Product.UsageRate,
                LabNote = canViewTechnicalFields && x.Trial != null ? x.Trial.LabNote : null,
                SampleRequestCreatedDate = x.SampleRequest.CreatedDate,
                CreatedDate = x.Trial != null ? x.Trial.CreatedDate : x.SampleRequest.CreatedDate,
                UpdatedDate = x.Trial != null ? x.Trial.UpdatedDate : x.SampleRequest.UpdatedDate
            })
            .ToListAsync(cancellationToken);
        timings.Mark("payload");
        if (historyDecisions is not null)
            foreach (var item in items)
            {
                item.WorkDecision = historyDecisions.GetValueOrDefault(item.SampleRequestSampleTrialId ?? item.SampleRequestId);
                item.NeedsLabSchedule = NeedsHistorySchedule(item);
            }
        if (dailyPayload is not null)
        {
            await dailyPayload.AttachAsync(items, _dbContext, scope.CompanyId, cancellationToken);
            items = items.OrderBy(dailyPayload.Position).ToList();
        }
        else if (workflowDetail)
            await SampleRequestDailyPayloadPage.AttachWorkflowFormulasAsync(pageQuery, items, _dbContext, scope.CompanyId, cancellationToken);
        timings.Mark("page-formulas");
        // Show the sales owners of the sample requests in each loaded customer group.
        var customerSaleNames = items.GroupBy(x => x.CustomerId)
            .ToDictionary(group => group.Key, group => string.Join(", ", group
                .Select(x => x.ManagerSalesName).Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct().OrderBy(name => name)));
        foreach (var item in items)
            item.CurrentCustomerSaleName = customerSaleNames.GetValueOrDefault(item.CustomerId);
        if (sortedCustomerIds is not null)
        {
            // Only reorder the bounded selected page; rows inside each customer retain SQL work priority.
            var customerPositions = sortedCustomerIds.Select((id, position) => (id, position)).ToDictionary(x => x.id, x => x.position);
            items = items.OrderBy(x => customerPositions[x.CustomerId]).ToList();
        }

        foreach (var item in items)
            item.LabTeamCodes = SampleRequestLabTeamRules.Resolve(item.LabCategoryCode);

        await SampleRequestCustomerPriceAudit.AttachAsync(_dbContext, scope.CompanyId, items, cancellationToken);
        timings.Mark("price-feedback");
        var pageIds = items.Select(x => x.SampleRequestId).Distinct().ToArray();
        var pageTrials = _dbContext.SampleRequestSampleTrials.AsNoTracking()
            .Where(x => x.SampleRequest.CompanyId == scope.CompanyId && pageIds.Contains(x.SampleRequestId));
        var optionalChipIds = items.Any(x => x.DeliveredSampleQuantityKg == 0 && x.SentDate.HasValue)
            ? (await Services.Workflow.ChipThenSampleRules.OptionalChips(pageTrials).Select(x => x.SampleRequestSampleTrialId)
                .Union(Services.Workflow.ChipThenSampleRules.PlannedChips(pageTrials, _dbContext.SampleRequestWorkflowEvents)
                    .Select(x => x.SampleRequestSampleTrialId)).ToArrayAsync(cancellationToken)).ToHashSet() : [];
        var canConfirmPrice = _permissions.HasPermission(ApplicationPermissions.PLM.ConfirmCustomerPrice);
        var canReceiveStandard = _permissions.HasPermission(ApplicationPermissions.PLM.ReceiveLabStandard);
        var editableIds = new HashSet<Guid>();
        if (items.Count > 0 && writeScope != null)
        {
            editableIds = (await _visibilityService.ApplySampleRequestVisibility(_dbContext.SampleRequests.AsNoTracking(),
                    _dbContext.Customers.AsNoTracking(), writeScope)
                .Where(x => pageIds.Contains(x.SampleRequestId) && x.CompanyId == scope.CompanyId && x.IsActive)
                .Select(x => x.SampleRequestId).ToListAsync(cancellationToken)).ToHashSet();
        }
        timings.Mark("write-permissions");
        foreach (var item in items)
        {
            item.CanActForSaleOwner = activeSaleActor && writeScope != null &&
                editableIds.Contains(item.SampleRequestId) &&
                SampleSaleWorkflowAccess.CanActFor(_currentUser, _permissions, item.ManagerSalesEmployeeId, writeScope);
            item.CanUpdateCustomerFeedback &= item.CanActForSaleOwner == true;
            item.IsChipFeedbackOptional = item.SampleRequestSampleTrialId is { } trialId && optionalChipIds.Contains(trialId);
            if (item.IsChipFeedbackOptional && item.SampleRequestStatus != "Cancelled" && item.SampleRequestStatus != "LabDeclined" && item.CanActForSaleOwner == true &&
                item.Status is SampleTrialStatus.SampleSent or SampleTrialStatus.WaitingCustomerFeedback or SampleTrialStatus.PriceQuote)
                item.CanUpdateCustomerFeedback = true;
            item.CanCorrectCustomerFeedback &= item.CanActForSaleOwner == true;
            var open = item.SampleRequestStatus is not "Completed" and not "Cancelled" and not "LabDeclined";
            // Each item already passed readable sample scope; shared notes do not require workflow ownership.
            item.CanWriteLabNote = open && activeLabNoteActor;
            item.CanConfirmCustomerPrice = open && item.RequiresCustomerPriceConfirmation && item.CustomerPriceConfirmation?.IsActive != true &&
                canConfirmPrice && item.CanActForSaleOwner == true;
            item.CanReceiveLabStandard = open && canReceiveStandard && editableIds.Contains(item.SampleRequestId);
            item.CanRevokeCustomerPrice = item.CanRevokeCustomerPrice && open && canConfirmPrice &&
                item.CanActForSaleOwner == true && item.CustomerPriceConfirmation?.IsActive == true;
            SampleRequestDailyWorkRules.Apply(item, request.DailyWorkDate ?? DateOnly.FromDateTime(DateTime.Today),
                false, canUpdateLabProgress, false, canUpdateCustomerFeedback, _currentUser.EmployeeId, !request.WorkAudience.HasValue);
            item.TurnaroundDays = SampleRequestSampleTrialReportRules.CalculateTurnaroundDays(
                item.LabReceivedDate,
                item.FinishedDate);
        }

        if (request.DailyWorkView is SampleRequestDailyView.SalePending or SampleRequestDailyView.SaleToday)
        {
            var ids = items.Select(x => x.SampleRequestId).ToArray();
            var dates = await SampleRequestSaleFollowUpQuery.Build(_crmDbContext, scope.CompanyId, _currentUser.EmployeeId ?? Guid.Empty)
                .Where(x => ids.Contains(x.SampleRequestId)).GroupBy(x => x.SampleRequestId)
                .Select(x => new { Id = x.Key, Date = x.Min(y => y.DueDate) }).ToListAsync(cancellationToken);
            foreach (var item in items) item.SaleFollowUpDate = dates.FirstOrDefault(x => x.Id == item.SampleRequestId)?.Date;
        }

        timings.Mark("page-permissions");
        await AttachStandardSellingPricesAsync(
            items,
            scope.CompanyId,
            request.NormalizedCurrency,
            _pricingVisibilityService.GetAccess(),
            workflowDetail,
            cancellationToken);
        timings.Mark("standard-prices");

        // Chỉ đọc khóa nguồn giá để tính công việc; không trả thêm giá/cost ngoài visibility hiện có.
        var workflowProducts = items.Select(x => x.ProductId).Distinct().ToArray();
        var workflowPrices = await _crmDbContext.ProductPricingVersions.AsNoTracking()
            .Where(x => x.CompanyId == scope.CompanyId && workflowProducts.Contains(x.ProductId) &&
                x.IsActive && x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0m &&
                x.Currency == request.NormalizedCurrency)
            .OrderByDescending(x => x.Version).ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new { x.ProductId, x.SourceFormulaId, At = (DateTime?)(x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate) }).ToListAsync(cancellationToken);
        var workflowPriceSources = workflowPrices.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First().SourceFormulaId);
        foreach (var item in items)
        {
            SampleRequestDailyWorkRules.Apply(item, request.DailyWorkDate ?? DateOnly.FromDateTime(DateTime.Today),
                workflowPriceSources.TryGetValue(item.ProductId, out var source) && SampleRequestPriceConfirmationBasis.HasApprovedPriceSource(source),
                canUpdateLabProgress, false, canUpdateCustomerFeedback, _currentUser.EmployeeId, !request.WorkAudience.HasValue);
            item.CanConfirmCustomerPrice &= item.LabFormulaConfirmedAt.HasValue && item.RealPriceQuoteDate.HasValue &&
                workflowPriceSources.TryGetValue(item.ProductId, out var acceptedSource) && SampleRequestPriceConfirmationBasis.HasApprovedPriceSource(acceptedSource);
        }

        timings.Mark("workflow-prices");
        await Services.Workflow.SampleFlowStateReader.AttachAsync(_dbContext, items, scope.CompanyId, _currentUser.EmployeeId,
            _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow),
            _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow), cancellationToken, workflowPriceSources,
            items.Where(x => x.CanActForSaleOwner == true).Select(x => x.SampleRequestId).ToHashSet(),
            workflowPrices.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First().At));
        timings.Mark("workflow-state");
        foreach (var item in items)
        {
            SampleRequestDailyWorkRules.Apply(item, request.DailyWorkDate ?? DateOnly.FromDateTime(DateTime.Today), false,
                canUpdateLabProgress, false, canUpdateCustomerFeedback, _currentUser.EmployeeId, !request.WorkAudience.HasValue);
            Services.SampleWorkAssignment.Apply(item, _currentUser, _permissions, request.WorkAudience);
            SampleRequestPriceFeedbackVisibility.Apply(item, _pricingVisibilityService.GetAccess());
        }
        return new PagedResult<SampleRequestSampleTrialReportDto>(
            items,
            leanPayload ? items.Count : totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize) { Facets = facets };
    }

    private async Task AttachStandardSellingPricesAsync(
        IReadOnlyCollection<SampleRequestSampleTrialReportDto> items,
        Guid companyId,
        string currency,
        PricingAccessDecision pricingAccess,
        bool workflowDetail,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0 ||
            (!pricingAccess.CanViewApprovedSellingPrice &&
             !pricingAccess.CanViewSystemCalculatedPrice))
        {
            return;
        }

        using var pricingTimings = SampleWorkDiagnosticTags.Start("sample_pricing");
        // Items have already passed company/customer visibility; no second TP lookup is needed.
        var productIds = items
            .Select(x => x.ProductId)
            .Distinct()
            .ToArray();
        pricingTimings?.Mark("target-products");
        if (productIds.Length == 0)
        {
            return;
        }

        Dictionary<Guid, ApprovedTrialPricingRow> approvedPricingByProduct = [];
        if (pricingAccess.CanViewApprovedSellingPrice)
        {
            var approvedPricingRows = await LatestApprovedPrices(_crmDbContext.ProductPricingVersions.AsNoTracking(),
                    companyId, productIds, currency)
                .Select(x => new ApprovedTrialPricingRow
                {
                    ProductId = x.ProductId,
                    StandardSellingPrice = x.StandardSellingPrice,
                    PublisherNote = x.PublisherNote,
                    ApprovedAt = x.ApprovedAt
                })
                .ToListAsync(cancellationToken);
            approvedPricingByProduct = approvedPricingRows
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.First());
        }
        pricingTimings?.Mark("approved-prices");

        IReadOnlyDictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>
            pricingSourcesByProduct =
                new Dictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>();
        if (pricingAccess.CanViewSystemCalculatedPrice)
        {
            pricingSourcesByProduct = workflowDetail
                ? await _pricingSourceQueryService.LoadForSampleTrialsVisibleAsync(
                    items.Select(x => (x.ProductId, x.FormulaId)).Distinct().ToArray(),
                    companyId, currency, pricingAccess, cancellationToken)
                : await _pricingSourceQueryService.LoadVisibleAsync(
                    productIds, companyId, currency, pricingAccess, cancellationToken);
        }
        pricingTimings?.Mark("realtime-sources");

        foreach (var item in items)
        {
            var productId = item.ProductId;
            approvedPricingByProduct.TryGetValue(productId, out var approvedPricing);

            var sources = pricingSourcesByProduct.GetValueOrDefault(productId) ?? [];
            var formulaSource = item.FormulaId.HasValue
                ? sources.FirstOrDefault(source =>
                    source.SourceType == ProductPricingSourceType.Formula &&
                    source.SourceId == item.FormulaId.Value &&
                    source.IsEligible)
                : null;
            var effectiveSource = formulaSource ?? sources.FirstOrDefault(source => source.IsEligible);
            SampleRequestTrialPricingVisibility.Apply(
                item,
                approvedPricing?.StandardSellingPrice,
                approvedPricing?.PublisherNote,
                approvedPricing?.ApprovedAt,
                effectiveSource?.StandardSellingPrice,
                pricingAccess);
        }
        pricingTimings?.Mark("apply");
    }

    private static bool NeedsHistorySchedule(SampleRequestSampleTrialReportDto item)
        => Services.Workflow.SampleFlowRules.IsOpen(item.SampleRequestStatus) && item.WorkDecision?.Role is "Lab" or "LabAdmin" &&
            (SampleRequestCustomerPriceRules.IsQuotation(item.InfoType) && item.RequestedSampleQuantity is not > 0
                ? item.ExpectedPriceQuoteDate == null : item.ExpectedDeliveryDate == null);

    internal static IQueryable<HRM.Domain.Entities.CustomerSchema.ProductPricingVersion> LatestApprovedPrices(
        IQueryable<HRM.Domain.Entities.CustomerSchema.ProductPricingVersion> source,
        Guid companyId, Guid[] productIds, string currency)
    {
        var eligible = source.Where(x => x.CompanyId == companyId && productIds.Contains(x.ProductId) &&
            x.Currency == currency && x.IsActive && x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0m);
        // Version is unique within company/product/currency. Select the winning row before materializing.
        return eligible.Where(x => !eligible.Any(newer => newer.ProductId == x.ProductId && newer.Version > x.Version));
    }

    private sealed class ApprovedTrialPricingRow
    {
        public Guid ProductId { get; init; }
        public decimal? StandardSellingPrice { get; init; }
        public string? PublisherNote { get; init; }
        public DateTime? ApprovedAt { get; init; }
    }

}
