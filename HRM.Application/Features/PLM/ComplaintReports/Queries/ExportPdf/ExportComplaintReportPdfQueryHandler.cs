using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries.ExportPdf;

internal sealed class ExportComplaintReportPdfQueryHandler
    : IRequestHandler<ExportComplaintReportPdfQuery, OperationResult<ComplaintReportPdfFileDto>>
{
    private const int MaxImages = 8;
    private const long MaxImageBytes = 3 * 1024 * 1024;
    private const long MaxTotalImageBytes = 12 * 1024 * 1024;

    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IAttachmentService _attachmentService;
    private readonly IComplaintReportPdfRenderer _renderer;
    private readonly ILogger<ExportComplaintReportPdfQueryHandler> _logger;

    public ExportComplaintReportPdfQueryHandler(
        IComplaintReportDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        IAttachmentService attachmentService,
        IComplaintReportPdfRenderer renderer,
        ILogger<ExportComplaintReportPdfQueryHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
        _attachmentService = attachmentService;
        _renderer = renderer;
        _logger = logger;
    }

    public async Task<OperationResult<ComplaintReportPdfFileDto>> Handle(
        ExportComplaintReportPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ComplaintReportId == Guid.Empty ||
            !ComplaintAuthorizationRules.CanViewPdf(_currentUser))
        {
            return OperationResult<ComplaintReportPdfFileDto>.Fail(
                "Complaint report was not found or is outside your visibility scope.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleCustomers = _visibilityService.ApplyCustomerVisibility(
            _dbContext.Customers.AsNoTracking(), scope);
        var document = await _dbContext.ComplaintReports.AsNoTracking()
            .Where(x => x.ComplaintReportId == request.ComplaintReportId &&
                x.CompanyId == scope.CompanyId && x.IsActive &&
                visibleCustomers.Any(customer => customer.CustomerId == x.CustomerId))
            .Select(x => new ComplaintReportPdfDocumentDto
            {
                ExternalId = x.ExternalId,
                Status = x.Status,
                IsDraft = x.Status != ComplaintReportStatus.Closed,
                CompanyName = x.Company.Name,
                CustomerExternalId = x.CustomerExternalIdSnapshot,
                CustomerName = x.CustomerNameSnapshot,
                IssuePartName = x.IssuePartNameSnapshot,
                ReportedAt = x.ReportedAt,
                ProposedCompletionAt = x.ProposedCompletionAt,
                ReporterName = x.CreatedByNameSnapshot,
                RelatedStandards = x.RelatedStandards.ToString(),
                RelatedScopes = x.RelatedScopes.ToString(),
                OtherRelatedStandard = x.OtherRelatedStandard,
                DocumentRequirement = x.DocumentRequirement,
                Summary = x.Summary,
                NonConformityDescription = x.NonConformityDescription,
                RootCause = x.RootCause,
                InterestedPartyComment = x.InterestedPartyComment,
                CausingParty = x.CausingPartySnapshot,
                RiskReviewedAt = x.RiskReviewedAt,
                HasNewRisk = x.HasNewRisk,
                RiskReviewComment = x.RiskReviewComment,
                EffectivenessPersonInChargeName = x.EffectivenessPersonInChargeNameSnapshot,
                EffectivenessReviewUntil = x.EffectivenessReviewUntil,
                HasRecurrence = x.HasRecurrence,
                EffectivenessConclusion = x.EffectivenessConclusion.HasValue
                    ? x.EffectivenessConclusion.Value.ToString()
                    : null,
                EffectivenessComment = x.EffectivenessComment
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return OperationResult<ComplaintReportPdfFileDto>.Fail(
                "Complaint report was not found or is outside your visibility scope.");
        }

        var header = await _dbContext.ComplaintReports.AsNoTracking()
            .Where(x => x.ComplaintReportId == request.ComplaintReportId &&
                x.CompanyId == scope.CompanyId && x.IsActive)
            .Select(x => new { x.ComplaintReportId, x.AttachmentCollectionId })
            .FirstAsync(cancellationToken);
        var lines = await LoadLinesAsync(header.ComplaintReportId, cancellationToken);
        var actions = await _dbContext.ComplaintCapaActions.AsNoTracking()
            .Where(x => x.ComplaintReportId == header.ComplaintReportId && x.IsActive)
            .OrderBy(x => x.ActionType).ThenBy(x => x.SortOrder)
            .Select(x => new
            {
                x.ActionType,
                Item = new ComplaintReportPdfActionDto
                {
                    SortOrder = x.SortOrder,
                    Content = x.Content,
                    PersonInChargeName = x.PersonInChargeNameSnapshot,
                    Deadline = x.Deadline,
                    Result = x.Result,
                    CompletedAt = x.CompletedAt
                }
            })
            .ToListAsync(cancellationToken);
        var approvals = await _dbContext.ComplaintReportApprovals.AsNoTracking()
            .Where(x => x.ComplaintReportId == header.ComplaintReportId && x.IsActive)
            .OrderBy(x => x.DecidedAt)
            .Select(x => new ComplaintReportPdfApprovalDto
            {
                Stage = x.Stage,
                Decision = x.Decision,
                ActorName = x.ActorNameSnapshot,
                DecidedAt = x.DecidedAt,
                Comment = x.Comment
            })
            .ToListAsync(cancellationToken);
        var attachmentResult = await LoadAttachmentsAsync(
            header.AttachmentCollectionId, cancellationToken);

        document = CopyWithCollections(
            document,
            lines,
            actions.Where(x => x.ActionType == ComplaintCapaActionType.Immediate)
                .Select(x => x.Item).ToList(),
            actions.Where(x => x.ActionType == ComplaintCapaActionType.CorrectivePreventive)
                .Select(x => x.Item).ToList(),
            approvals,
            attachmentResult.Attachments,
            attachmentResult.Images);
        var content = _renderer.Render(document);
        return OperationResult<ComplaintReportPdfFileDto>.Ok(new ComplaintReportPdfFileDto
        {
            FileName = ComplaintReportPdfRules.BuildFileName(document.ExternalId),
            Content = content
        });
    }

    private async Task<IReadOnlyList<ComplaintReportPdfLineDto>> LoadLinesAsync(
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.ComplaintReportLines.AsNoTracking()
            .Where(x => x.ComplaintReportId == reportId && x.IsActive)
            .OrderBy(x => x.CreatedDate)
            .ThenBy(x => x.ComplaintReportLineId)
            .Select(x => new ComplaintReportPdfLineDto
            {
                SourceOrderExternalId = x.SourceMerchandiseOrderDetail.MerchandiseOrder.ExternalId,
                ProductExternalId = x.ProductExternalIdSnapshot,
                ProductName = x.ProductNameSnapshot,
                FormulaExternalId = x.FormulaExternalIdSnapshot,
                ManufacturingFormulaExternalId = x.ManufacturingFormulaExternalIdSnapshot,
                ComplaintQuantity = x.ComplaintQuantity,
                ApprovedReplacementQuantity = x.ApprovedReplacementQuantity,
                IssueType = x.IssueType,
                Severity = x.Severity,
                Description = x.Description
            })
            .ToListAsync(cancellationToken);
        var lineIds = await _dbContext.ComplaintReportLines.AsNoTracking()
            .Where(x => x.ComplaintReportId == reportId && x.IsActive)
            .OrderBy(x => x.CreatedDate)
            .ThenBy(x => x.ComplaintReportLineId)
            .Select(x => x.ComplaintReportLineId)
            .ToListAsync(cancellationToken);
        var lots = await _dbContext.ComplaintReportLineLots.AsNoTracking()
            .Where(x => lineIds.Contains(x.ComplaintReportLineId) && x.IsActive)
            .OrderBy(x => x.CreatedDate)
            .Select(x => new
            {
                x.ComplaintReportLineId,
                Lot = new ComplaintReportPdfLotDto
                {
                    LotNo = x.LotNoSnapshot,
                    ComplaintQuantity = x.ComplaintQuantity,
                    DeliveredQuantity = x.DeliveredQuantitySnapshot,
                    DeliveredAt = x.DeliveredAtSnapshot
                }
            })
            .ToListAsync(cancellationToken);
        var byLine = lots.GroupBy(x => x.ComplaintReportLineId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<ComplaintReportPdfLotDto>)x.Select(y => y.Lot).ToList());
        return rows.Select((line, index) => CopyLine(
            line,
            byLine.GetValueOrDefault(lineIds[index], Array.Empty<ComplaintReportPdfLotDto>())))
            .ToList();
    }

    private async Task<AttachmentLoadResult> LoadAttachmentsAsync(
        Guid collectionId,
        CancellationToken cancellationToken)
    {
        var attachments = await _attachmentService.ListAsync(collectionId, null, cancellationToken);
        var images = new List<ComplaintReportPdfImageDto>();
        var documents = new List<ComplaintReportPdfAttachmentDto>(attachments.Count);
        long totalImageBytes = 0;
        foreach (var attachment in attachments.OrderBy(x => x.CreateDate))
        {
            var embedded = false;
            if (attachment.IsImage && images.Count < MaxImages &&
                attachment.SizeBytes <= MaxImageBytes &&
                totalImageBytes + attachment.SizeBytes <= MaxTotalImageBytes)
            {
                try
                {
                    var content = await _attachmentService.GetContentAsync(
                        attachment.AttachmentId, cancellationToken);
                    await using var stream = content.Stream;
                    using var memory = new MemoryStream((int)Math.Min(content.Length, int.MaxValue));
                    await stream.CopyToAsync(memory, cancellationToken);
                    var bytes = memory.ToArray();
                    if (ComplaintReportPdfRules.IsSupportedImage(bytes))
                    {
                        images.Add(new ComplaintReportPdfImageDto
                        {
                            FileName = attachment.FileName,
                            Content = bytes
                        });
                        totalImageBytes += bytes.Length;
                        embedded = true;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        exception,
                        "Could not embed complaint attachment {AttachmentId} in PDF.",
                        attachment.AttachmentId);
                }
            }

            documents.Add(new ComplaintReportPdfAttachmentDto
            {
                FileName = attachment.FileName,
                SizeBytes = attachment.SizeBytes,
                IsImage = attachment.IsImage,
                IsEmbedded = embedded
            });
        }

        return new AttachmentLoadResult(documents, images);
    }

    private static ComplaintReportPdfLineDto CopyLine(
        ComplaintReportPdfLineDto line,
        IReadOnlyList<ComplaintReportPdfLotDto> lots)
        => new()
        {
            SourceOrderExternalId = line.SourceOrderExternalId,
            ProductExternalId = line.ProductExternalId,
            ProductName = line.ProductName,
            FormulaExternalId = line.FormulaExternalId,
            ManufacturingFormulaExternalId = line.ManufacturingFormulaExternalId,
            ComplaintQuantity = line.ComplaintQuantity,
            ApprovedReplacementQuantity = line.ApprovedReplacementQuantity,
            IssueType = line.IssueType,
            Severity = line.Severity,
            Description = line.Description,
            Lots = lots
        };

    private static ComplaintReportPdfDocumentDto CopyWithCollections(
        ComplaintReportPdfDocumentDto source,
        IReadOnlyList<ComplaintReportPdfLineDto> lines,
        IReadOnlyList<ComplaintReportPdfActionDto> immediate,
        IReadOnlyList<ComplaintReportPdfActionDto> corrective,
        IReadOnlyList<ComplaintReportPdfApprovalDto> approvals,
        IReadOnlyList<ComplaintReportPdfAttachmentDto> attachments,
        IReadOnlyList<ComplaintReportPdfImageDto> images)
        => new()
        {
            FormCode = source.FormCode,
            ExternalId = source.ExternalId,
            Status = source.Status,
            IsDraft = source.IsDraft,
            CompanyName = source.CompanyName,
            CustomerExternalId = source.CustomerExternalId,
            CustomerName = source.CustomerName,
            IssuePartName = source.IssuePartName,
            ReportedAt = source.ReportedAt,
            ProposedCompletionAt = source.ProposedCompletionAt,
            ReporterName = source.ReporterName,
            RelatedStandards = source.RelatedStandards,
            RelatedScopes = source.RelatedScopes,
            OtherRelatedStandard = source.OtherRelatedStandard,
            DocumentRequirement = source.DocumentRequirement,
            Summary = source.Summary,
            NonConformityDescription = source.NonConformityDescription,
            RootCause = source.RootCause,
            InterestedPartyComment = source.InterestedPartyComment,
            CausingParty = source.CausingParty,
            RiskReviewedAt = source.RiskReviewedAt,
            HasNewRisk = source.HasNewRisk,
            RiskReviewComment = source.RiskReviewComment,
            EffectivenessPersonInChargeName = source.EffectivenessPersonInChargeName,
            EffectivenessReviewUntil = source.EffectivenessReviewUntil,
            HasRecurrence = source.HasRecurrence,
            EffectivenessConclusion = source.EffectivenessConclusion,
            EffectivenessComment = source.EffectivenessComment,
            Lines = lines,
            ImmediateActions = immediate,
            CorrectivePreventiveActions = corrective,
            Approvals = approvals,
            Attachments = attachments,
            Images = images
        };

    private sealed record AttachmentLoadResult(
        IReadOnlyList<ComplaintReportPdfAttachmentDto> Attachments,
        IReadOnlyList<ComplaintReportPdfImageDto> Images);
}
