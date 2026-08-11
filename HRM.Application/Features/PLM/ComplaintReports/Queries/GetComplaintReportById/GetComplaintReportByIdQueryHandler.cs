using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries.GetComplaintReportById;

internal sealed class GetComplaintReportByIdQueryHandler
    : IRequestHandler<GetComplaintReportByIdQuery, ComplaintReportDetailDto?>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IAttachmentService _attachmentService;
    private readonly ICurrentUser _currentUser;

    public GetComplaintReportByIdQueryHandler(
        IComplaintReportDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        IAttachmentService attachmentService,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _attachmentService = attachmentService;
        _currentUser = currentUser;
    }

    public async Task<ComplaintReportDetailDto?> Handle(
        GetComplaintReportByIdQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleCustomers = _visibilityService.ApplyCustomerVisibility(
            _dbContext.Customers.AsNoTracking(),
            scope);

        var header = await _dbContext.ComplaintReports
            .AsNoTracking()
            .Where(x =>
                x.ComplaintReportId == request.ComplaintReportId &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive &&
                visibleCustomers.Any(customer => customer.CustomerId == x.CustomerId))
            .Select(x => new
            {
                x.ComplaintReportId,
                x.ExternalId,
                x.AttachmentCollectionId,
                x.Status,
                x.RequestedResolutionType,
                x.RequestedReplacementDeliveryDate,
                x.ResolutionType,
                x.CustomerId,
                CustomerExternalId = x.CustomerExternalIdSnapshot != string.Empty
                    ? x.CustomerExternalIdSnapshot
                    : x.Customer.ExternalId,
                CustomerName = x.CustomerNameSnapshot != string.Empty
                    ? x.CustomerNameSnapshot
                    : x.Customer.CustomerName,
                x.IssuePartId,
                x.IssuePartNameSnapshot,
                x.RelatedStandards,
                x.RelatedScopes,
                x.OtherRelatedStandard,
                x.Summary,
                x.DocumentRequirement,
                x.NonConformityDescription,
                x.RootCause,
                x.InterestedPartyComment,
                x.CausingPartId,
                x.CausingPartySnapshot,
                x.ResolutionNote,
                x.ReportedAt,
                x.ProposedCompletionAt,
                x.RiskReviewedAt,
                x.HasNewRisk,
                x.RiskReviewComment,
                x.EffectivenessPersonInChargeId,
                x.EffectivenessPersonInChargeNameSnapshot,
                x.EffectivenessReviewUntil,
                x.HasRecurrence,
                x.EffectivenessConclusion,
                x.EffectivenessComment,
                x.CompletedAt,
                CompletedByName = x.CompletedByNavigation != null ? x.CompletedByNavigation.FullName : null,
                x.CreatedBy,
                CreatedByName = x.CreatedByNameSnapshot != string.Empty
                    ? x.CreatedByNameSnapshot
                    : x.CreatedByNavigation.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (header is null)
        {
            return null;
        }

        var lines = await _dbContext.ComplaintReportLines
            .AsNoTracking()
            .Where(x => x.ComplaintReportId == header.ComplaintReportId && x.IsActive)
            .OrderBy(x => x.CreatedDate)
            .Select(x => new ComplaintReportLineDto
            {
                ComplaintReportLineId = x.ComplaintReportLineId,
                SourceMerchandiseOrderDetailId = x.SourceMerchandiseOrderDetailId,
                SourceMerchandiseOrderId = x.SourceMerchandiseOrderDetail.MerchandiseOrderId,
                SourceMerchandiseOrderExternalId = x.SourceMerchandiseOrderDetail.MerchandiseOrder.ExternalId,
                ProductId = x.ProductId,
                ProductExternalId = x.ProductExternalIdSnapshot,
                ProductName = x.ProductNameSnapshot,
                FormulaId = x.FormulaId,
                FormulaExternalId = x.FormulaExternalIdSnapshot,
                SourceMfgProductionOrderId = x.SourceMfgProductionOrderId,
                SourceMfgProductionOrderExternalId = x.SourceMfgProductionOrder != null
                    ? x.SourceMfgProductionOrder.ExternalId
                    : null,
                ManufacturingFormulaId = x.ManufacturingFormulaId,
                ManufacturingFormulaExternalId = x.ManufacturingFormulaExternalIdSnapshot,
                ComplaintQuantity = x.ComplaintQuantity,
                ApprovedReplacementQuantity = x.ApprovedReplacementQuantity,
                IssueType = x.IssueType,
                Severity = x.Severity,
                Description = x.Description,
                ResolutionNote = x.ResolutionNote
            })
            .ToListAsync(cancellationToken);

        var lineIds = lines.Select(x => x.ComplaintReportLineId).ToArray();
        var lots = await _dbContext.ComplaintReportLineLots
            .AsNoTracking()
            .Where(x => x.IsActive && lineIds.Contains(x.ComplaintReportLineId))
            .OrderBy(x => x.CreatedDate)
            .Select(x => new
            {
                x.ComplaintReportLineId,
                Item = new ComplaintReportLineLotDto
                {
                    ComplaintReportLineLotId = x.ComplaintReportLineLotId,
                    SourceDeliveryOrderDetailId = x.SourceDeliveryOrderDetailId,
                    SourceLotConsumptionId = x.SourceLotConsumptionId,
                    LotNo = x.LotNoSnapshot,
                    DeliveredQuantity = x.DeliveredQuantitySnapshot,
                    ComplaintQuantity = x.ComplaintQuantity,
                    DeliveredAt = x.DeliveredAtSnapshot
                }
            })
            .ToListAsync(cancellationToken);
        var lotsByLine = lots
            .GroupBy(x => x.ComplaintReportLineId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<ComplaintReportLineLotDto>)x.Select(row => row.Item).ToList());
        lines = lines.Select(line => new ComplaintReportLineDto
        {
            ComplaintReportLineId = line.ComplaintReportLineId,
            SourceMerchandiseOrderDetailId = line.SourceMerchandiseOrderDetailId,
            SourceMerchandiseOrderId = line.SourceMerchandiseOrderId,
            SourceMerchandiseOrderExternalId = line.SourceMerchandiseOrderExternalId,
            ProductId = line.ProductId,
            ProductExternalId = line.ProductExternalId,
            ProductName = line.ProductName,
            FormulaId = line.FormulaId,
            FormulaExternalId = line.FormulaExternalId,
            SourceMfgProductionOrderId = line.SourceMfgProductionOrderId,
            SourceMfgProductionOrderExternalId = line.SourceMfgProductionOrderExternalId,
            ManufacturingFormulaId = line.ManufacturingFormulaId,
            ManufacturingFormulaExternalId = line.ManufacturingFormulaExternalId,
            ComplaintQuantity = line.ComplaintQuantity,
            ApprovedReplacementQuantity = line.ApprovedReplacementQuantity,
            IssueType = line.IssueType,
            Severity = line.Severity,
            Description = line.Description,
            ResolutionNote = line.ResolutionNote,
            Lots = lotsByLine.GetValueOrDefault(line.ComplaintReportLineId, Array.Empty<ComplaintReportLineLotDto>())
        }).ToList();

        var actions = await _dbContext.ComplaintCapaActions
            .AsNoTracking()
            .Where(x => x.ComplaintReportId == header.ComplaintReportId && x.IsActive)
            .OrderBy(x => x.ActionType)
            .ThenBy(x => x.SortOrder)
            .Select(x => new
            {
                x.ActionType,
                Item = new ComplaintCapaActionDto
                {
                    ComplaintCapaActionId = x.ComplaintCapaActionId,
                    ActionType = x.ActionType.ToString(),
                    SortOrder = x.SortOrder,
                    Content = x.Content,
                    PersonInChargeId = x.PersonInChargeId,
                    PersonInChargeName = x.PersonInChargeNameSnapshot,
                    Deadline = x.Deadline,
                    Result = x.Result,
                    CompletedAt = x.CompletedAt
                }
            })
            .ToListAsync(cancellationToken);

        var approvals = await _dbContext.ComplaintReportApprovals
            .AsNoTracking()
            .Where(x => x.ComplaintReportId == header.ComplaintReportId && x.IsActive)
            .OrderBy(x => x.DecidedAt)
            .Select(x => new ComplaintApprovalDto
            {
                ComplaintReportApprovalId = x.ComplaintReportApprovalId,
                Stage = x.Stage.ToString(),
                Decision = x.Decision.ToString(),
                ActorId = x.ActorId,
                ActorName = x.ActorNameSnapshot,
                DecidedAt = x.DecidedAt,
                Comment = x.Comment
            })
            .ToListAsync(cancellationToken);

        var handlingHeader = await _dbContext.MerchandiseOrders
            .AsNoTracking()
            .Where(x =>
                x.ComplaintReportId == header.ComplaintReportId &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive)
            .Select(x => new
            {
                x.MerchandiseOrderId,
                x.ExternalId,
                x.Status,
                x.TotalPrice,
                x.CreateDate
            })
            .FirstOrDefaultAsync(cancellationToken);

        ComplaintHandlingOrderDto? handlingOrder = null;
        if (handlingHeader is not null)
        {
            var handlingLineRows = await _dbContext.MerchandiseOrderDetails
                .AsNoTracking()
                .Where(x => x.MerchandiseOrderId == handlingHeader.MerchandiseOrderId && x.IsActive)
                .Select(x => new
                {
                    x.MerchandiseOrderDetailId,
                    x.ComplaintReportLineId,
                    ProductExternalId = x.ProductExternalIdSnapshot,
                    ProductName = x.ProductNameSnapshot,
                    Quantity = x.ExpectedQuantity,
                    x.Status
                })
                .ToListAsync(cancellationToken);
            var handlingDetailIds = handlingLineRows.Select(x => x.MerchandiseOrderDetailId).ToArray();
            var handlingMfgRows = await _dbContext.MfgOrderPOs
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    handlingDetailIds.Contains(x.MerchandiseOrderDetailId) &&
                    x.ProductionOrder.CompanyId == scope.CompanyId)
                .Select(x => new
                {
                    x.MerchandiseOrderDetailId,
                    x.MfgProductionOrderId,
                    ExternalId = x.ProductionOrder.ExternalId,
                    x.ProductionOrder.Status,
                    x.ProductionOrder.CreatedDate
                })
                .ToListAsync(cancellationToken);
            var handlingMfgByDetail = handlingMfgRows
                .GroupBy(x => x.MerchandiseOrderDetailId)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(row => row.CreatedDate).First());
            var handlingLines = handlingLineRows.Select(line =>
            {
                handlingMfgByDetail.TryGetValue(line.MerchandiseOrderDetailId, out var mfg);
                return new ComplaintHandlingOrderLineDto
                {
                    MerchandiseOrderDetailId = line.MerchandiseOrderDetailId,
                    ComplaintReportLineId = line.ComplaintReportLineId,
                    ProductExternalId = line.ProductExternalId,
                    ProductName = line.ProductName,
                    Quantity = line.Quantity,
                    Status = line.Status,
                    MfgProductionOrderId = mfg?.MfgProductionOrderId,
                    MfgProductionOrderExternalId = mfg?.ExternalId,
                    MfgStatus = mfg?.Status
                };
            }).ToList();
            handlingOrder = new ComplaintHandlingOrderDto
            {
                MerchandiseOrderId = handlingHeader.MerchandiseOrderId,
                ExternalId = handlingHeader.ExternalId,
                Status = handlingHeader.Status,
                TotalPrice = handlingHeader.TotalPrice ?? 0,
                CreatedDate = handlingHeader.CreateDate,
                Lines = handlingLines
            };
        }

        var attachments = await _attachmentService.ListAsync(
            header.AttachmentCollectionId,
            null,
            cancellationToken);
        var isTerminal = header.Status is ComplaintReportStatus.Closed
            or ComplaintReportStatus.Rejected
            or ComplaintReportStatus.Cancelled;
        var canInvestigate = ComplaintAuthorizationRules.CanInvestigate(_currentUser);
        var canApprove = ComplaintAuthorizationRules.CanFinalApprove(_currentUser);
        var canManageActions = ComplaintAuthorizationRules.CanManageActions(_currentUser);
        var canVerify = ComplaintAuthorizationRules.CanVerify(_currentUser);
        var hasAssignedAction = actions.Any(x =>
            x.Item.PersonInChargeId == scope.EmployeeId &&
            x.Item.CompletedAt is null);
        return new ComplaintReportDetailDto
        {
            ComplaintReportId = header.ComplaintReportId,
            ExternalId = header.ExternalId,
            AttachmentCollectionId = header.AttachmentCollectionId,
            Status = header.Status.ToString(),
            RequestedResolutionType = header.RequestedResolutionType?.ToString(),
            RequestedReplacementDeliveryDate = header.RequestedReplacementDeliveryDate,
            ResolutionType = header.ResolutionType?.ToString(),
            CustomerId = header.CustomerId,
            CustomerExternalId = header.CustomerExternalId,
            CustomerName = header.CustomerName,
            IssuePartId = header.IssuePartId,
            IssuePartName = header.IssuePartNameSnapshot,
            RelatedStandards = header.RelatedStandards.ToString(),
            RelatedScopes = header.RelatedScopes.ToString(),
            OtherRelatedStandard = header.OtherRelatedStandard,
            Summary = header.Summary,
            DocumentRequirement = header.DocumentRequirement,
            NonConformityDescription = header.NonConformityDescription,
            RootCause = header.RootCause,
            InterestedPartyComment = header.InterestedPartyComment,
            CausingPartId = header.CausingPartId,
            CausingParty = header.CausingPartySnapshot,
            ResolutionNote = header.ResolutionNote,
            ReportedAt = header.ReportedAt,
            ProposedCompletionAt = header.ProposedCompletionAt,
            Reception = new ComplaintReceptionDto
            {
                IssuePartId = header.IssuePartId,
                IssuePartName = header.IssuePartNameSnapshot,
                RelatedStandards = header.RelatedStandards.ToString(),
                RelatedScopes = header.RelatedScopes.ToString(),
                OtherRelatedStandard = header.OtherRelatedStandard,
                DocumentRequirement = header.DocumentRequirement,
                ReportedAt = header.ReportedAt,
                ProposedCompletionAt = header.ProposedCompletionAt,
                ReportedById = header.CreatedBy,
                ReportedByName = header.CreatedByName
            },
            Investigation = new ComplaintInvestigationDto
            {
                NonConformityDescription = header.NonConformityDescription,
                RootCause = header.RootCause,
                InterestedPartyComment = header.InterestedPartyComment,
                CausingPartId = header.CausingPartId,
                CausingParty = header.CausingPartySnapshot,
                ResolutionNote = header.ResolutionNote
            },
            RiskReview = new ComplaintRiskReviewDto
            {
                ReviewedAt = header.RiskReviewedAt,
                HasNewRisk = header.HasNewRisk,
                Comment = header.RiskReviewComment
            },
            EffectivenessVerification = new ComplaintEffectivenessDto
            {
                PersonInChargeId = header.EffectivenessPersonInChargeId,
                PersonInChargeName = header.EffectivenessPersonInChargeNameSnapshot,
                ReviewUntil = header.EffectivenessReviewUntil,
                HasRecurrence = header.HasRecurrence,
                Conclusion = header.EffectivenessConclusion?.ToString(),
                Comment = header.EffectivenessComment
            },
            CompletedAt = header.CompletedAt,
            CompletedByName = header.CompletedByName,
            Lines = lines,
            ImmediateActions = actions
                .Where(x => x.ActionType == ComplaintCapaActionType.Immediate)
                .Select(x => x.Item)
                .ToList(),
            CorrectivePreventiveActions = actions
                .Where(x => x.ActionType == ComplaintCapaActionType.CorrectivePreventive)
                .Select(x => x.Item)
                .ToList(),
            ApprovalHistory = approvals,
            HandlingMerchandiseOrderId = handlingOrder?.MerchandiseOrderId,
            HandlingMerchandiseOrderExternalId = handlingOrder?.ExternalId,
            HandlingOrder = handlingOrder,
            Attachments = attachments,
            AllowedActions = new ComplaintAllowedActionsDto
            {
                CanEditReception = header.Status == ComplaintReportStatus.Draft && header.CreatedBy == scope.EmployeeId,
                CanResubmit = header.Status == ComplaintReportStatus.Draft &&
                    header.CreatedBy == scope.EmployeeId && ComplaintAuthorizationRules.CanCreate(_currentUser),
                CanInvestigate = canInvestigate && header.Status is ComplaintReportStatus.Submitted
                    or ComplaintReportStatus.Investigating or ComplaintReportStatus.ActionInProgress,
                CanManageActions = canManageActions && ComplaintWorkflowRules.CanReplaceActions(header.Status),
                CanUpdateAssignedActions = hasAssignedAction &&
                    ComplaintWorkflowRules.CanUpdateActionResult(header.Status),
                CanVerifyEffectiveness = canVerify &&
                    ComplaintWorkflowRules.CanSaveEffectiveness(header.Status),
                CanInitialDecision = ComplaintAuthorizationRules.CanInitialApprove(_currentUser) &&
                    ComplaintDecisionRules.CanMakeInitialDecision(header.Status),
                CanFinalDecision = canApprove &&
                    ComplaintDecisionRules.CanMakeFinalDecision(header.Status),
                CanViewPdf = ComplaintAuthorizationRules.CanViewPdf(_currentUser)
            }
        };
    }
}
