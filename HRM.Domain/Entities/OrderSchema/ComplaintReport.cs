using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Orders;

namespace HRM.Domain.Entities.OrderSchema;

public partial class ComplaintReport
{
    public Guid ComplaintReportId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerExternalIdSnapshot { get; set; } = string.Empty;
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public Guid AttachmentCollectionId { get; set; }
    public Guid? IssuePartId { get; set; }
    public string? IssuePartNameSnapshot { get; set; }
    public ComplaintReportStatus Status { get; set; }
    public ComplaintResolutionType? RequestedResolutionType { get; set; }
    public DateTime? RequestedReplacementDeliveryDate { get; set; }
    public ComplaintResolutionType? ResolutionType { get; set; }
    public ComplaintRelatedStandard RelatedStandards { get; set; }
    public ComplaintRelatedScope RelatedScopes { get; set; }
    public string? OtherRelatedStandard { get; set; }
    public string? Summary { get; set; }
    public string? DocumentRequirement { get; set; }
    public string? NonConformityDescription { get; set; }
    public string? RootCause { get; set; }
    public string? InterestedPartyComment { get; set; }
    public Guid? CausingPartId { get; set; }
    public string? CausingPartySnapshot { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime? ProposedCompletionAt { get; set; }
    public DateTime? RiskReviewedAt { get; set; }
    public bool? HasNewRisk { get; set; }
    public string? RiskReviewComment { get; set; }
    public Guid? EffectivenessPersonInChargeId { get; set; }
    public string? EffectivenessPersonInChargeNameSnapshot { get; set; }
    public DateTime? EffectivenessReviewUntil { get; set; }
    public bool? HasRecurrence { get; set; }
    public ComplaintEffectivenessConclusion? EffectivenessConclusion { get; set; }
    public string? EffectivenessComment { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public string CreatedByNameSnapshot { get; set; } = string.Empty;
    public DateTime UpdatedDate { get; set; }
    public Guid UpdatedBy { get; set; }

    public virtual Company Company { get; set; } = null!;
    public virtual Customer Customer { get; set; } = null!;
    public virtual AttachmentCollection AttachmentCollection { get; set; } = null!;
    public virtual Part? IssuePart { get; set; }
    public virtual Part? CausingPart { get; set; }
    public virtual Employee? EffectivenessPersonInCharge { get; set; }
    public virtual Employee CreatedByNavigation { get; set; } = null!;
    public virtual Employee UpdatedByNavigation { get; set; } = null!;
    public virtual Employee? CompletedByNavigation { get; set; }
    public virtual ICollection<ComplaintReportLine> ComplaintReportLines { get; set; } = new List<ComplaintReportLine>();
    public virtual ICollection<ComplaintCapaAction> CapaActions { get; set; } = new List<ComplaintCapaAction>();
    public virtual ICollection<ComplaintReportApproval> Approvals { get; set; } = new List<ComplaintReportApproval>();
    public virtual ICollection<MerchandiseOrder> ProcessingMerchandiseOrders { get; set; } = new List<MerchandiseOrder>();
}
