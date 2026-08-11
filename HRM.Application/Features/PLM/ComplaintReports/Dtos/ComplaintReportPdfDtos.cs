using HRM.Domain.Enums.Orders;

namespace HRM.Application.Features.PLM.ComplaintReports.Dtos;

public sealed class ComplaintReportPdfFileDto
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/pdf";
    public byte[] Content { get; init; } = [];
}

public sealed class ComplaintReportPdfDocumentDto
{
    public string FormCode { get; init; } = "VA-QMR-F31(05)";
    public string ExternalId { get; init; } = string.Empty;
    public ComplaintReportStatus Status { get; init; }
    public bool IsDraft { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string? IssuePartName { get; init; }
    public DateTime ReportedAt { get; init; }
    public DateTime? ProposedCompletionAt { get; init; }
    public string ReporterName { get; init; } = string.Empty;
    public string RelatedStandards { get; init; } = string.Empty;
    public string RelatedScopes { get; init; } = string.Empty;
    public string? OtherRelatedStandard { get; init; }
    public string? DocumentRequirement { get; init; }
    public string? Summary { get; init; }
    public string? NonConformityDescription { get; init; }
    public string? RootCause { get; init; }
    public string? InterestedPartyComment { get; init; }
    public string? CausingParty { get; init; }
    public DateTime? RiskReviewedAt { get; init; }
    public bool? HasNewRisk { get; init; }
    public string? RiskReviewComment { get; init; }
    public string? EffectivenessPersonInChargeName { get; init; }
    public DateTime? EffectivenessReviewUntil { get; init; }
    public bool? HasRecurrence { get; init; }
    public string? EffectivenessConclusion { get; init; }
    public string? EffectivenessComment { get; init; }
    public IReadOnlyList<ComplaintReportPdfLineDto> Lines { get; init; } = [];
    public IReadOnlyList<ComplaintReportPdfActionDto> ImmediateActions { get; init; } = [];
    public IReadOnlyList<ComplaintReportPdfActionDto> CorrectivePreventiveActions { get; init; } = [];
    public IReadOnlyList<ComplaintReportPdfApprovalDto> Approvals { get; init; } = [];
    public IReadOnlyList<ComplaintReportPdfAttachmentDto> Attachments { get; init; } = [];
    public IReadOnlyList<ComplaintReportPdfImageDto> Images { get; init; } = [];
}

public sealed class ComplaintReportPdfLineDto
{
    public string SourceOrderExternalId { get; init; } = string.Empty;
    public string ProductExternalId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string FormulaExternalId { get; init; } = string.Empty;
    public string? ManufacturingFormulaExternalId { get; init; }
    public decimal ComplaintQuantity { get; init; }
    public decimal? ApprovedReplacementQuantity { get; init; }
    public string? IssueType { get; init; }
    public string? Severity { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<ComplaintReportPdfLotDto> Lots { get; init; } = [];
}

public sealed class ComplaintReportPdfLotDto
{
    public string LotNo { get; init; } = string.Empty;
    public decimal ComplaintQuantity { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public DateTime? DeliveredAt { get; init; }
}

public sealed class ComplaintReportPdfActionDto
{
    public int SortOrder { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? PersonInChargeName { get; init; }
    public DateTime? Deadline { get; init; }
    public string? Result { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed class ComplaintReportPdfApprovalDto
{
    public ComplaintApprovalStage Stage { get; init; }
    public ComplaintApprovalDecision Decision { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public DateTime DecidedAt { get; init; }
    public string? Comment { get; init; }
}

public sealed class ComplaintReportPdfAttachmentDto
{
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public bool IsImage { get; init; }
    public bool IsEmbedded { get; init; }
}

public sealed class ComplaintReportPdfImageDto
{
    public string FileName { get; init; } = string.Empty;
    public byte[] Content { get; init; } = [];
}
