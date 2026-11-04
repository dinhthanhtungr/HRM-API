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

    public GetSampleRequestSampleTrialsQueryHandler(
        IPLMReadDbContext dbContext,
        ICRMReadDbContext crmDbContext,
        ICustomerVisibilityService visibilityService,
        IPLMFieldVisibilityService fieldVisibility,
        ICurrentUser currentUser,
        ProductPricingSourceQueryService pricingSourceQueryService,
        IPricingVisibilityService pricingVisibilityService,
        ICurrentUserPermissionService permissions,
        Services.SampleRequestLabTeamScope labTeams, Services.SampleWorkPreviewSession? preview = null)
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
    }

    public async Task<PagedResult<SampleRequestSampleTrialReportDto>> Handle(
        GetSampleRequestSampleTrialsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.WorkAudience is { } workAudience && !SampleRequestWorkQueueAccess.CanRead(_currentUser, workAudience))
            throw new UnauthorizedAccessException("The current user cannot access this sample work queue.");
        var scope = await _visibilityService.BuildReadScopeAsync(cancellationToken);
        var activeSaleActor = _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow) &&
            _currentUser.EmployeeId is { } actorEmployee && actorEmployee != Guid.Empty &&
            await _dbContext.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == actorEmployee &&
                x.CompanyId == scope.CompanyId && x.IsActive, cancellationToken);
        ViewerScope? writeScope = null;
        if (activeSaleActor || _permissions.HasPermission(ApplicationPermissions.PLM.ConfirmCustomerPrice) ||
            _permissions.HasPermission(ApplicationPermissions.PLM.ReceiveLabStandard))
            writeScope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var allowedSaleOwners = activeSaleActor && writeScope != null
            ? SampleSaleWorkflowAccess.GetAllowedOwnerIds(_currentUser, _permissions, writeScope) : [];
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

        if (request.SampleRequestId.HasValue)
        {
            visibleSampleRequests = visibleSampleRequests.Where(
                x => x.SampleRequestId == request.SampleRequestId.Value);
        }

        // Tất cả là tra cứu cả hồ sơ cũ; company/customer/ownership scope vẫn giữ nguyên.
        if (request.UseOperationalScope || (request.WorkAudience.HasValue && request.WorkBucket != SampleRequestWorkBucket.All) ||
            (!request.WorkAudience.HasValue && request.DailyWorkDate.HasValue && request.DailyWorkView != SampleRequestDailyView.All))
            visibleSampleRequests = SampleRequestWorkScope.Apply(visibleSampleRequests, _dbContext.SampleRequestWorkflowEvents);

        if (request.AdminMetric is { } adminMetric && request.AdminPeriodFrom is { } adminFrom && request.AdminPeriodTo is { } adminTo)
        {
            if (!HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestReportAccess.CanRead(_currentUser))
                throw new UnauthorizedAccessException("Sample reporting requires an authenticated employee and company.");
            if (!HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminReportQuery.IsValidPeriod(adminFrom, adminTo) || !Enum.IsDefined(adminMetric))
                throw new ArgumentException("Invalid admin report period or metric.");
            var adminRows = HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminReportQuery
                .Build(visibleSampleRequests, _dbContext);
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
            visibleSampleRequests = SampleRequestWorkQueueQuery.ApplySaleOwnership(visibleSampleRequests, writable, allowedSaleOwners);
        }
        var saleEmployeeId = request.SaleEmployeeId;
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

        var trialCounts =
            from trial in activeTrials
            group trial by trial.SampleRequestId into trialGroup
            select new
            {
                SampleRequestId = trialGroup.Key,
                Count = trialGroup.Count(),
                LatestTrialNo = trialGroup.Max(x => x.TrialNo)
            };

        var latestTrials =
            from trial in activeTrials
            join trialCount in trialCounts
                on new { trial.SampleRequestId, trial.TrialNo }
                equals new { trialCount.SampleRequestId, TrialNo = trialCount.LatestTrialNo }
            select trial;

        var reportTrials = request.IncludeTrialHistory ||
            (request.DailyWorkDate == null && request.ReportType == SampleTrialReportType.WaitingCustomerFeedback)
            ? activeTrials : latestTrials;

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

        query = SampleRequestLabWorkQuery.Enrich(query, _dbContext);
        query = Services.SampleWorkDecisionQuery.Enrich(query, _dbContext, _crmDbContext, request.NormalizedCurrency);

        query = SampleRequestSampleTrialReportQueryRules.ApplyReportType(query, request);
        if (request.WorkAudience.HasValue)
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

        if (!string.IsNullOrWhiteSpace(request.NormalizedKeyword))
        {
            var keyword = request.NormalizedKeyword;
            if (request.DailyWorkDate.HasValue)
            {
                foreach (var token in keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var tokenPattern = PostgresSearchPattern.ContainsLiteral(token);
                    query = query.Where(x =>
                        EF.Functions.ILike(x.SampleRequest.ManagerByNavigation.FullName, tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
                        EF.Functions.ILike(x.SampleRequest.Customer.CustomerName, tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
                        EF.Functions.ILike(x.SampleRequest.ExternalId, tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
                        EF.Functions.ILike(x.SampleRequest.Product.Name ?? string.Empty, tokenPattern, PostgresSearchPattern.EscapeCharacter) ||
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
                EF.Functions.ILike(((x.Trial != null ? x.Trial.SampleRequestExternalIdSnapshot : null) ?? x.SampleRequest.ExternalId), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(((x.Trial != null ? x.Trial.ProductNameSnapshot : null) ?? x.SampleRequest.Product.Name ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(((x.Trial != null ? x.Trial.ColourCodeSnapshot : null) ?? x.SampleRequest.Product.ColourCode ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                (x.SampleRequest.Formula != null && EF.Functions.ILike(x.SampleRequest.Formula.ExternalId, $"%{keyword}%")) ||
                (x.Trial != null && x.Trial.Formula != null && EF.Functions.ILike(x.Trial.Formula.ExternalId, $"%{keyword}%")) ||
                (x.Trial != null && EF.Functions.ILike((x.Trial.BatchNo ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                (x.Trial != null && EF.Functions.ILike((x.Trial.CustomerReplyNote ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                (x.Trial != null && EF.Functions.ILike((x.Trial.LabNote ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)));
            }
        }

        query = SampleRequestSampleTrialReportQueryRules.ApplySorting(query, request);
        var totalCount = await query.CountAsync(cancellationToken);
        var canViewTechnicalFields = _fieldVisibility.CanViewProductTechnicalInfo();
        var canUpdateCustomerFeedback = activeSaleActor;
        var canUpdateExpectedPriceQuoteDate =
            _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SampleRequestExpectedPriceQuoteDateEditors);
        var canUpdateLabProgress = _currentUser.EmployeeId.HasValue && _currentUser.EmployeeId != Guid.Empty &&
            _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SampleRequestLabProgressEditors);

        var canManageLegacyWork = _currentUser.EmployeeId is { } employeeId && employeeId != Guid.Empty &&
            _permissions.HasPermission(ApplicationPermissions.PLM.ManageLegacySampleWork);
        var canCorrectFeedback = _permissions.HasPermission(ApplicationPermissions.PLM.CorrectSampleTrialFeedback);
        var items = await query
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .Select(x => new SampleRequestSampleTrialReportDto
            {
                WorkDecision = x.WorkDecision,
                HasAvailableFormula = _dbContext.Formulas.Any(f => f.CompanyId == x.SampleRequest.CompanyId &&
                    f.ProductId == x.SampleRequest.ProductId && f.IsActive),
                NeedsLabSchedule = x.NeedsSchedule && (x.WorkDecision!.Role == "Lab" || x.WorkDecision.Role == "LabAdmin"),
                CanCorrectCustomerFeedback = canCorrectFeedback && allowedSaleOwners.Contains(x.SampleRequest.ManagerBy) &&
                    x.Trial != null && x.Trial.CustomerReplyDate.HasValue &&
                    !_dbContext.SampleRequestSampleTrials.Any(newer => newer.SampleRequestId == x.SampleRequest.SampleRequestId &&
                        newer.IsActive && newer.TrialNo > x.Trial.TrialNo),
                IsLegacyWork = x.SampleRequest.CreatedDate < SampleRequestWorkScope.StartedAt,
                IsLegacyWorkTracked = x.SampleRequest.CreatedDate < SampleRequestWorkScope.StartedAt &&
                    _dbContext.SampleRequestWorkflowEvents.Where(e => e.CompanyId == x.SampleRequest.CompanyId &&
                        e.SampleRequestId == x.SampleRequest.SampleRequestId &&
                        (e.EventType == SampleRequestWorkScope.LegacyIncluded || e.EventType == SampleRequestWorkScope.LegacyExcluded))
                        .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id)
                        .Select(e => e.EventType).FirstOrDefault() == SampleRequestWorkScope.LegacyIncluded,
                CanManageLegacyWorkTracking = x.SampleRequest.CreatedDate < SampleRequestWorkScope.StartedAt &&
                    canManageLegacyWork,
                SampleRequestSampleTrialId = x.Trial != null ? x.Trial.SampleRequestSampleTrialId : null,
                SampleRequestId = x.SampleRequest.SampleRequestId,
                ProductId = x.SampleRequest.ProductId,
                ManagerSalesEmployeeId = x.SampleRequest.ManagerBy,
                InfoType = x.SampleRequest.InfoType,
                FormulaStatus = _dbContext.Formulas.Where(f => f.FormulaId == ((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId) && f.CompanyId == x.SampleRequest.CompanyId && f.ProductId == x.SampleRequest.ProductId && f.IsActive).Select(f => f.Status).FirstOrDefault(),
                RealPriceQuoteDate = x.SampleRequest.RealPriceQuoteDate,
                FormulaId = (x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId,
                PriceConfirmationFormulaId = (x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId,
                FormulaExternalId = _dbContext.Formulas.Where(f => f.FormulaId == ((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId) && f.CompanyId == x.SampleRequest.CompanyId && f.ProductId == x.SampleRequest.ProductId && f.IsActive).Select(f => f.ExternalId).FirstOrDefault(),
                TrialNo = x.Trial != null ? x.Trial.TrialNo : null,
                HasTrial = x.Trial != null,
                TrialCount = x.TrialCount ?? 0,
                HasPreviousTrials = (x.TrialCount ?? 0) > 1,
                CanCreateTrial = canViewTechnicalFields && x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt &&
                    x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled",
                CanUpdateTrial = canViewTechnicalFields && x.Trial != null,
                CanUpdateExpectedPriceQuoteDate = canUpdateExpectedPriceQuoteDate && x.Trial != null,
                CanUpdateCustomerFeedback = canUpdateCustomerFeedback && allowedSaleOwners.Contains(x.SampleRequest.ManagerBy) &&
                    x.Trial != null &&
                    x.SampleRequest.Status != "Cancelled" &&
                    (x.Trial.Status == SampleTrialStatus.SampleSent ||
                     x.Trial.Status == SampleTrialStatus.WaitingCustomerFeedback ||
                     x.Trial.Status == SampleTrialStatus.PriceQuote),

                SampleRequestStatus = x.SampleRequest.Status,
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
                LabFormulaConfirmedAt = x.LabFormulaConfirmedAt,
                SampleRequestUpdatedDate = x.SampleRequest.UpdatedDate,
                CanUpdateLabProgress = canUpdateLabProgress &&
                    x.SampleRequest.Status != "Completed" && x.SampleRequest.Status != "Cancelled",
                FormulaCreatedByEmployeeId = _dbContext.Formulas.Where(f => f.FormulaId == ((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId) && f.CompanyId == x.SampleRequest.CompanyId && f.ProductId == x.SampleRequest.ProductId && f.IsActive).Select(f => f.CreatedBy).FirstOrDefault(),
                FormulaCreatedByName = _dbContext.Formulas.Where(f => f.FormulaId == ((x.SampleRequest.CreatedDate < Services.Workflow.SampleFlowRules.StartsAt ? x.SampleRequest.FormulaId : x.Trial != null ? x.Trial.FormulaId : null) ?? x.ConfirmedFormulaId ?? x.SampleRequest.FormulaId) && f.CompanyId == x.SampleRequest.CompanyId && f.ProductId == x.SampleRequest.ProductId && f.IsActive).Where(f => f.CreatedByNavigation != null).Select(f => f.CreatedByNavigation!.FullName).FirstOrDefault(),
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

        foreach (var item in items)
            item.LabTeamCodes = SampleRequestLabTeamRules.Resolve(item.LabCategoryCode);

        await SampleRequestCustomerPriceAudit.AttachAsync(_dbContext, scope.CompanyId, items, cancellationToken);
        var pageIds = items.Select(x => x.SampleRequestId).ToArray();
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
        foreach (var item in items)
        {
            item.CanActForSaleOwner = activeSaleActor && writeScope != null &&
                editableIds.Contains(item.SampleRequestId) &&
                SampleSaleWorkflowAccess.CanActFor(_currentUser, _permissions, item.ManagerSalesEmployeeId, writeScope);
            item.CanUpdateCustomerFeedback &= item.CanActForSaleOwner == true;
            item.CanCorrectCustomerFeedback &= item.CanActForSaleOwner == true;
            var open = item.SampleRequestStatus is not "Completed" and not "Cancelled";
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

        await AttachStandardSellingPricesAsync(
            items,
            scope.CompanyId,
            request.NormalizedCurrency,
            _pricingVisibilityService.GetAccess(),
            cancellationToken);

        // Chỉ đọc khóa nguồn giá để tính công việc; không trả thêm giá/cost ngoài visibility hiện có.
        var workflowProducts = items.Select(x => x.ProductId).Distinct().ToArray();
        var workflowPrices = await _crmDbContext.ProductPricingVersions.AsNoTracking()
            .Where(x => x.CompanyId == scope.CompanyId && workflowProducts.Contains(x.ProductId) &&
                x.IsActive && x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0m &&
                x.Currency == request.NormalizedCurrency)
            .OrderByDescending(x => x.Version).ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new { x.ProductId, x.SourceFormulaId }).ToListAsync(cancellationToken);
        var workflowPriceSources = workflowPrices.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First().SourceFormulaId);
        foreach (var item in items)
        {
            SampleRequestDailyWorkRules.Apply(item, request.DailyWorkDate ?? DateOnly.FromDateTime(DateTime.Today),
                workflowPriceSources.TryGetValue(item.ProductId, out var source) && SampleRequestPriceConfirmationBasis.MatchesApprovedFormula(item, source),
                canUpdateLabProgress, false, canUpdateCustomerFeedback, _currentUser.EmployeeId, !request.WorkAudience.HasValue);
            item.CanConfirmCustomerPrice &= item.LabFormulaConfirmedAt.HasValue && item.RealPriceQuoteDate.HasValue &&
                workflowPriceSources.TryGetValue(item.ProductId, out var acceptedSource) && SampleRequestPriceConfirmationBasis.MatchesApprovedFormula(item, acceptedSource);
        }

        await Services.Workflow.SampleFlowStateReader.AttachAsync(_dbContext, items, scope.CompanyId, _currentUser.EmployeeId,
            _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow),
            _permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow), cancellationToken, workflowPriceSources,
            items.Where(x => x.CanActForSaleOwner == true).Select(x => x.SampleRequestId).ToHashSet());
        foreach (var item in items)
        {
            SampleRequestDailyWorkRules.Apply(item, request.DailyWorkDate ?? DateOnly.FromDateTime(DateTime.Today), false,
                canUpdateLabProgress, false, canUpdateCustomerFeedback, _currentUser.EmployeeId, !request.WorkAudience.HasValue);
            Services.SampleWorkAssignment.Apply(item, _currentUser, _permissions, request.WorkAudience);
            SampleRequestPriceFeedbackVisibility.Apply(item, _pricingVisibilityService.GetAccess());
        }
        return new PagedResult<SampleRequestSampleTrialReportDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private async Task AttachStandardSellingPricesAsync(
        IReadOnlyCollection<SampleRequestSampleTrialReportDto> items,
        Guid companyId,
        string currency,
        PricingAccessDecision pricingAccess,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0 ||
            (!pricingAccess.CanViewApprovedSellingPrice &&
             !pricingAccess.CanViewSystemCalculatedPrice))
        {
            return;
        }

        var sampleRequestIds = items
            .Select(x => x.SampleRequestId)
            .Distinct()
            .ToArray();
        var productRows = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                sampleRequestIds.Contains(x.SampleRequestId) &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new
            {
                x.SampleRequestId,
                x.ProductId
            })
            .ToListAsync(cancellationToken);
        var productBySampleRequest = productRows.ToDictionary(
            x => x.SampleRequestId,
            x => x.ProductId);
        var productIds = productRows
            .Select(x => x.ProductId)
            .Distinct()
            .ToArray();
        if (productIds.Length == 0)
        {
            return;
        }

        Dictionary<Guid, ApprovedTrialPricingRow> approvedPricingByProduct = [];
        if (pricingAccess.CanViewApprovedSellingPrice)
        {
            var approvedPricingRows = await _crmDbContext.ProductPricingVersions
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    productIds.Contains(x.ProductId) &&
                    x.Currency == currency &&
                    x.IsActive &&
                    x.Status == ProductPricingStatus.Approved &&
                    x.StandardSellingPrice > 0m)
                .OrderByDescending(x => x.Version)
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

        IReadOnlyDictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>
            pricingSourcesByProduct =
                new Dictionary<Guid, IReadOnlyList<ProductPricingSourceOptionDto>>();
        if (pricingAccess.CanViewSystemCalculatedPrice)
        {
            pricingSourcesByProduct = await _pricingSourceQueryService.LoadVisibleAsync(
                productIds,
                companyId,
                currency,
                pricingAccess,
                cancellationToken);
        }

        foreach (var item in items)
        {
            if (!productBySampleRequest.TryGetValue(item.SampleRequestId, out var productId))
            {
                continue;
            }

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
    }

    private sealed class ApprovedTrialPricingRow
    {
        public Guid ProductId { get; init; }
        public decimal? StandardSellingPrice { get; init; }
        public string? PublisherNote { get; init; }
        public DateTime? ApprovedAt { get; init; }
    }

}
