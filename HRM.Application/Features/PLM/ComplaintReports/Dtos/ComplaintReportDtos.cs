using HRM.Domain.Enums.Orders;
using HRM.Application.Features.Attachments.Dtos;

namespace HRM.Application.Features.PLM.ComplaintReports.Dtos;

public sealed record ComplaintReportLineRequest
{
    public Guid SourceMerchandiseOrderDetailId { get; init; }
    public decimal ComplaintQuantity { get; init; }
    public string? IssueType { get; init; }
    public string? Severity { get; init; }
    public string? Description { get; init; }
    public IReadOnlyCollection<ComplaintReportLotRequest> Lots { get; init; } = Array.Empty<ComplaintReportLotRequest>();
}

public sealed record ComplaintReportLotRequest
{
    public Guid SourceDeliveryOrderDetailId { get; init; }
    public Guid? SourceLotConsumptionId { get; init; }
    public decimal ComplaintQuantity { get; init; }
}

public sealed record CreateComplaintReportRequest
{
    public Guid CustomerId { get; init; }
    public string? Summary { get; init; }
    public string? NonConformityDescription { get; init; }
    public ComplaintResolutionType RequestedResolutionType { get; init; } = ComplaintResolutionType.FeedbackOnly;
    public DateTime? RequestedReplacementDeliveryDate { get; init; }
    public IReadOnlyCollection<ComplaintReportLineRequest> Lines { get; init; } = Array.Empty<ComplaintReportLineRequest>();
}

public sealed record UpdateComplaintReceptionRequest
{
    public string? Summary { get; init; }
    public string? NonConformityDescription { get; init; }
    public ComplaintResolutionType RequestedResolutionType { get; init; } = ComplaintResolutionType.FeedbackOnly;
    public DateTime? RequestedReplacementDeliveryDate { get; init; }
    public IReadOnlyCollection<ComplaintReportLineRequest> Lines { get; init; } = Array.Empty<ComplaintReportLineRequest>();
}

public sealed record UpdateComplaintInvestigationRequest
{
    public Guid? IssuePartId { get; init; }
    public ComplaintRelatedStandard RelatedStandards { get; init; }
    public string? OtherRelatedStandard { get; init; }
    public ComplaintRelatedScope RelatedScopes { get; init; }
    public string? DocumentRequirement { get; init; }
    public string? NonConformityDescription { get; init; }
    public string? RootCause { get; init; }
    public string? InterestedPartyComment { get; init; }
    public Guid? CausingPartId { get; init; }
    public string? CausingParty { get; init; }
    public bool? HasNewRisk { get; init; }
    public string? RiskReviewComment { get; init; }
}

public sealed record ReplaceComplaintCapaActionsRequest
{
    public IReadOnlyCollection<ComplaintCapaActionRequest> Immediate { get; init; } =
        Array.Empty<ComplaintCapaActionRequest>();
    public IReadOnlyCollection<ComplaintCapaActionRequest> CorrectivePreventive { get; init; } =
        Array.Empty<ComplaintCapaActionRequest>();
}

public sealed record ComplaintCapaActionRequest
{
    public string? Content { get; init; }
    public Guid PersonInChargeId { get; init; }
    public DateTime? Deadline { get; init; }
    public int SortOrder { get; init; }
}

public sealed record UpdateComplaintCapaActionResultRequest
{
    public string? Result { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed record UpdateComplaintEffectivenessRequest
{
    public Guid PersonInChargeId { get; init; }
    public DateTime ReviewUntil { get; init; }
    public bool HasRecurrence { get; init; }
    public ComplaintEffectivenessConclusion Conclusion { get; init; }
    public string? Comment { get; init; }
}

public sealed record ComplaintDecisionLineRequest
{
    public Guid ComplaintReportLineId { get; init; }
    public decimal? ApprovedReplacementQuantity { get; init; }
}

public sealed record InitialComplaintDecisionRequest
{
    public ComplaintApprovalDecision Decision { get; init; }
    public string? Comment { get; init; }
    public ComplaintResolutionType? ResolutionType { get; init; }
    public IReadOnlyCollection<ComplaintDecisionLineRequest> Lines { get; init; } =
        Array.Empty<ComplaintDecisionLineRequest>();
}

public sealed record FinalComplaintDecisionRequest
{
    public ComplaintApprovalDecision Decision { get; init; }
    public string? Comment { get; init; }
}

public class ComplaintReportResultDto
{
    public Guid ComplaintReportId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public Guid AttachmentCollectionId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? RequestedResolutionType { get; init; }
    public DateTime? RequestedReplacementDeliveryDate { get; init; }
    public string? ResolutionType { get; init; }
    public Guid? HandlingMerchandiseOrderId { get; init; }
    public string? HandlingMerchandiseOrderExternalId { get; init; }
}

public sealed class ComplaintReportDetailDto : ComplaintReportResultDto
{
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public Guid? IssuePartId { get; init; }
    public string? IssuePartName { get; init; }
    public string RelatedStandards { get; init; } = string.Empty;
    public string RelatedScopes { get; init; } = string.Empty;
    public string? OtherRelatedStandard { get; init; }
    public string? DocumentRequirement { get; init; }
    public string? NonConformityDescription { get; init; }
    public string? RootCause { get; init; }
    public string? InterestedPartyComment { get; init; }
    public Guid? CausingPartId { get; init; }
    public string? CausingParty { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTime ReportedAt { get; init; }
    public DateTime? ProposedCompletionAt { get; init; }
    public ComplaintReceptionDto Reception { get; init; } = new();
    public ComplaintInvestigationDto Investigation { get; init; } = new();
    public ComplaintRiskReviewDto RiskReview { get; init; } = new();
    public ComplaintEffectivenessDto EffectivenessVerification { get; init; } = new();
    public DateTime? CompletedAt { get; init; }
    public string? CompletedByName { get; init; }
    public IReadOnlyList<ComplaintReportLineDto> Lines { get; init; } = Array.Empty<ComplaintReportLineDto>();
    public IReadOnlyList<ComplaintCapaActionDto> ImmediateActions { get; init; } = Array.Empty<ComplaintCapaActionDto>();
    public IReadOnlyList<ComplaintCapaActionDto> CorrectivePreventiveActions { get; init; } = Array.Empty<ComplaintCapaActionDto>();
    public IReadOnlyList<ComplaintApprovalDto> ApprovalHistory { get; init; } = Array.Empty<ComplaintApprovalDto>();
    public ComplaintHandlingOrderDto? HandlingOrder { get; init; }
    public IReadOnlyList<AttachmentDto> Attachments { get; init; } = Array.Empty<AttachmentDto>();
    public ComplaintAllowedActionsDto AllowedActions { get; init; } = new();
}

public sealed class ComplaintReportLineDto
{
    public Guid ComplaintReportLineId { get; init; }
    public Guid SourceMerchandiseOrderDetailId { get; init; }
    public Guid SourceMerchandiseOrderId { get; init; }
    public string SourceMerchandiseOrderExternalId { get; init; } = string.Empty;
    public Guid ProductId { get; init; }
    public string ProductExternalId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public Guid FormulaId { get; init; }
    public string FormulaExternalId { get; init; } = string.Empty;
    public Guid? SourceMfgProductionOrderId { get; init; }
    public string? SourceMfgProductionOrderExternalId { get; init; }
    public Guid? ManufacturingFormulaId { get; init; }
    public string? ManufacturingFormulaExternalId { get; init; }
    public decimal ComplaintQuantity { get; init; }
    public decimal? ApprovedReplacementQuantity { get; init; }
    public string? IssueType { get; init; }
    public string? Severity { get; init; }
    public string? Description { get; init; }
    public string? ResolutionNote { get; init; }
    public IReadOnlyList<ComplaintReportLineLotDto> Lots { get; init; } = Array.Empty<ComplaintReportLineLotDto>();
}

public sealed class ComplaintSourceLineDto
{
    public Guid MerchandiseOrderId { get; init; }
    public string MerchandiseOrderExternalId { get; init; } = string.Empty;
    public DateTime OrderCreatedDate { get; init; }
    public Guid MerchandiseOrderDetailId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductExternalId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public Guid FormulaId { get; init; }
    public string FormulaExternalId { get; init; } = string.Empty;
    public decimal OrderedQuantity { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal ActiveComplaintQuantity { get; init; }
    public decimal RemainingComplaintableQuantity { get; init; }
    public string BagType { get; init; } = string.Empty;
    public string PackageWeight { get; init; } = string.Empty;
    public Guid? SourceMfgProductionOrderId { get; init; }
    public string? SourceMfgProductionOrderExternalId { get; init; }
    public Guid? ManufacturingFormulaId { get; init; }
    public string? ManufacturingFormulaExternalId { get; init; }
    public IReadOnlyList<ComplaintSourceDeliveryDto> Deliveries { get; init; } = Array.Empty<ComplaintSourceDeliveryDto>();
}

public sealed class ComplaintReportListItemDto
{
    public Guid ComplaintReportId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? RequestedResolutionType { get; init; }
    public string? ResolutionType { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public DateTime ReportedAt { get; init; }
    public decimal ComplaintQuantity { get; init; }
    public int LineCount { get; init; }
    public bool HasHandlingOrder { get; init; }
    public bool HasOpenActions { get; init; }
    public string TimelineBadge { get; init; } = string.Empty;
    public Guid? HandlingMerchandiseOrderId { get; init; }
    public string? HandlingMerchandiseOrderExternalId { get; init; }
}

public sealed class ComplaintSourceDeliveryDto
{
    public Guid DeliveryOrderDetailId { get; init; }
    public Guid DeliveryOrderId { get; init; }
    public string DeliveryOrderExternalId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal DeliveredQuantity { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public IReadOnlyList<ComplaintSourceLotDto> Lots { get; init; } = Array.Empty<ComplaintSourceLotDto>();
}

public sealed class ComplaintSourceLotDto
{
    public Guid? LotConsumptionId { get; init; }
    public string LotNo { get; init; } = string.Empty;
    public decimal DeliveredQuantity { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public decimal ActiveComplaintQuantity { get; init; }
    public decimal RemainingComplaintableQuantity { get; init; }
}

public sealed class ComplaintReportLineLotDto
{
    public Guid ComplaintReportLineLotId { get; init; }
    public Guid SourceDeliveryOrderDetailId { get; init; }
    public Guid? SourceLotConsumptionId { get; init; }
    public string LotNo { get; init; } = string.Empty;
    public decimal DeliveredQuantity { get; init; }
    public decimal ComplaintQuantity { get; init; }
    public DateTime? DeliveredAt { get; init; }
}

public sealed class ComplaintCapaActionDto
{
    public Guid ComplaintCapaActionId { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public string Content { get; init; } = string.Empty;
    public Guid? PersonInChargeId { get; init; }
    public string? PersonInChargeName { get; init; }
    public DateTime? Deadline { get; init; }
    public string? Result { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed class ComplaintRiskReviewDto
{
    public DateTime? ReviewedAt { get; init; }
    public bool? HasNewRisk { get; init; }
    public string? Comment { get; init; }
}

public sealed class ComplaintReceptionDto
{
    public Guid? IssuePartId { get; init; }
    public string? IssuePartName { get; init; }
    public string RelatedStandards { get; init; } = string.Empty;
    public string RelatedScopes { get; init; } = string.Empty;
    public string? OtherRelatedStandard { get; init; }
    public string? DocumentRequirement { get; init; }
    public DateTime ReportedAt { get; init; }
    public DateTime? ProposedCompletionAt { get; init; }
    public Guid ReportedById { get; init; }
    public string ReportedByName { get; init; } = string.Empty;
}

public sealed class ComplaintInvestigationDto
{
    public string? NonConformityDescription { get; init; }
    public string? RootCause { get; init; }
    public string? InterestedPartyComment { get; init; }
    public Guid? CausingPartId { get; init; }
    public string? CausingParty { get; init; }
    public string? ResolutionNote { get; init; }
}

public sealed class ComplaintEffectivenessDto
{
    public Guid? PersonInChargeId { get; init; }
    public string? PersonInChargeName { get; init; }
    public DateTime? ReviewUntil { get; init; }
    public bool? HasRecurrence { get; init; }
    public string? Conclusion { get; init; }
    public string? Comment { get; init; }
}

public sealed class ComplaintApprovalDto
{
    public Guid ComplaintReportApprovalId { get; init; }
    public string Stage { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public Guid ActorId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public DateTime DecidedAt { get; init; }
    public string? Comment { get; init; }
}

public sealed class ComplaintHandlingOrderDto
{
    public Guid MerchandiseOrderId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal TotalPrice { get; init; }
    public DateTime CreatedDate { get; init; }
    public IReadOnlyList<ComplaintHandlingOrderLineDto> Lines { get; init; } = Array.Empty<ComplaintHandlingOrderLineDto>();
}

public sealed class ComplaintHandlingOrderLineDto
{
    public Guid MerchandiseOrderDetailId { get; init; }
    public Guid? ComplaintReportLineId { get; init; }
    public string ProductExternalId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? MfgProductionOrderId { get; init; }
    public string? MfgProductionOrderExternalId { get; init; }
    public string? MfgStatus { get; init; }
}

public sealed class ComplaintAllowedActionsDto
{
    public bool CanEditReception { get; init; }
    public bool CanResubmit { get; init; }
    public bool CanInvestigate { get; init; }
    public bool CanManageActions { get; init; }
    public bool CanUpdateAssignedActions { get; init; }
    public bool CanVerifyEffectiveness { get; init; }
    public bool CanInitialDecision { get; init; }
    public bool CanFinalDecision { get; init; }
    public bool CanViewPdf { get; init; }
}
