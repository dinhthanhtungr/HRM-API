using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.CreateComplaintReport;

/// <summary>
/// Records a sale-submitted customer complaint from delivered lines and lots. The backend owns status,
/// snapshots and manufacturing traceability; this command never decides the final resolution or creates handling work.
/// </summary>
internal sealed class CreateComplaintReportCommandHandler
    : IRequestHandler<CreateComplaintReportCommand, OperationResult<ComplaintReportResultDto>>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IExternalIdService _externalIdService;
    private readonly IAttachmentService _attachmentService;
    private readonly IFileStorage _fileStorage;
    private readonly ComplaintReceptionResolver _receptionResolver;
    private readonly ComplaintInteractionWriter _interactionWriter;
    private readonly ILogger<CreateComplaintReportCommandHandler> _logger;

    public CreateComplaintReportCommandHandler(
        IComplaintReportDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IExternalIdService externalIdService,
        IAttachmentService attachmentService,
        IFileStorage fileStorage,
        ComplaintReceptionResolver receptionResolver,
        ComplaintInteractionWriter interactionWriter,
        ILogger<CreateComplaintReportCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _externalIdService = externalIdService;
        _attachmentService = attachmentService;
        _fileStorage = fileStorage;
        _receptionResolver = receptionResolver;
        _interactionWriter = interactionWriter;
        _logger = logger;
    }

    public async Task<OperationResult<ComplaintReportResultDto>> Handle(
        CreateComplaintReportCommand command,
        CancellationToken cancellationToken)
    {
        if (!ComplaintAuthorizationRules.CanCreate(_currentUser))
        {
            return OperationResult<ComplaintReportResultDto>.Fail("Bạn không có quyền tiếp nhận khiếu nại.");
        }

        var request = command.Request;
        if (request.CustomerId == Guid.Empty)
        {
            return OperationResult<ComplaintReportResultDto>.Fail("Khách hàng là bắt buộc.");
        }

        var validationError = ComplaintReceptionRequestValidator.Validate(
            request.Summary,
            request.NonConformityDescription,
            request.RequestedResolutionType,
            request.Lines);
        if (validationError is not null)
        {
            return OperationResult<ComplaintReportResultDto>.Fail(validationError);
        }

        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        var now = _dateTimeProvider.Now;
        IReadOnlyList<AttachmentDto> uploadedAttachments = Array.Empty<AttachmentDto>();
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var resolvedResult = await _receptionResolver.ResolveAsync(
                request.CustomerId,
                request.Lines,
                null,
                cancellationToken);
            if (!resolvedResult.Success || resolvedResult.Data is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult<ComplaintReportResultDto>.Fail(
                    resolvedResult.Message ?? "Dữ liệu tiếp nhận khiếu nại không hợp lệ.");
            }

            var creatorName = await _dbContext.Employees
                .AsNoTracking()
                .Where(x =>
                    x.EmployeeId == employeeId &&
                    x.CompanyId == companyId &&
                    x.IsActive)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(creatorName))
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult<ComplaintReportResultDto>.Fail(
                    "Không tìm thấy nhân viên hiện tại trong công ty.");
            }

            var resolved = resolvedResult.Data;
            var report = new ComplaintReport
            {
                ComplaintReportId = Guid.CreateVersion7(),
                ExternalId = await _externalIdService.GenerateMonthlyCodeAsync(
                    companyId,
                    DocumentPrefix.CPL.ToString(),
                    cancellationToken),
                CompanyId = companyId,
                CustomerId = resolved.CustomerId,
                CustomerExternalIdSnapshot = resolved.CustomerExternalId,
                CustomerNameSnapshot = resolved.CustomerName,
                AttachmentCollectionId = Guid.CreateVersion7(),
                Status = ComplaintReportStatus.Submitted,
                RequestedResolutionType = request.RequestedResolutionType,
                RequestedReplacementDeliveryDate = request.RequestedReplacementDeliveryDate,
                ResolutionType = null,
                Summary = request.Summary!.Trim(),
                NonConformityDescription = Normalize(request.NonConformityDescription),
                ReportedAt = now,
                IsActive = true,
                CreatedDate = now,
                CreatedBy = employeeId,
                CreatedByNameSnapshot = creatorName.Trim(),
                UpdatedDate = now,
                UpdatedBy = employeeId
            };

            await _dbContext.AttachmentCollections.AddAsync(new AttachmentCollection
            {
                AttachmentCollectionId = report.AttachmentCollectionId
            }, cancellationToken);

            foreach (var resolvedLine in resolved.Lines)
            {
                var line = new ComplaintReportLine
                {
                    ComplaintReportLineId = Guid.CreateVersion7(),
                    ComplaintReportId = report.ComplaintReportId,
                    SourceMerchandiseOrderDetailId = resolvedLine.Source.DetailId,
                    SourceMfgProductionOrderId = resolvedLine.SourceMfgProductionOrderId,
                    ProductId = resolvedLine.Source.ProductId,
                    FormulaId = resolvedLine.Source.FormulaId,
                    ManufacturingFormulaId = resolvedLine.ManufacturingFormulaId,
                    ProductExternalIdSnapshot = resolvedLine.Source.ProductExternalId,
                    ProductNameSnapshot = resolvedLine.Source.ProductName,
                    FormulaExternalIdSnapshot = resolvedLine.Source.FormulaExternalId,
                    ManufacturingFormulaExternalIdSnapshot = resolvedLine.ManufacturingFormulaExternalId,
                    ComplaintQuantity = resolvedLine.ComplaintQuantity,
                    IssueType = resolvedLine.IssueType,
                    Severity = resolvedLine.Severity,
                    Description = resolvedLine.Description,
                    IsActive = true,
                    CreatedDate = now,
                    CreatedBy = employeeId,
                    UpdatedDate = now,
                    UpdatedBy = employeeId
                };

                foreach (var resolvedLot in resolvedLine.Lots)
                {
                    line.Lots.Add(new ComplaintReportLineLot
                    {
                        ComplaintReportLineLotId = Guid.CreateVersion7(),
                        ComplaintReportLineId = line.ComplaintReportLineId,
                        SourceDeliveryOrderDetailId = resolvedLot.SourceDeliveryOrderDetailId,
                        SourceLotConsumptionId = resolvedLot.SourceLotConsumptionId,
                        LotNoSnapshot = resolvedLot.LotNo,
                        DeliveredQuantitySnapshot = resolvedLot.DeliveredQuantity,
                        ComplaintQuantity = resolvedLot.ComplaintQuantity,
                        DeliveredAtSnapshot = resolvedLot.DeliveredAt,
                        IsActive = true,
                        CreatedDate = now,
                        CreatedBy = employeeId,
                        UpdatedDate = now,
                        UpdatedBy = employeeId
                    });
                }

                report.ComplaintReportLines.Add(line);
            }

            await _dbContext.ComplaintReports.AddAsync(report, cancellationToken);
            await _interactionWriter.AddAsync(
                report,
                BuildInteractionContent(report),
                employeeId,
                now,
                resolved.Lines
                    .Select(x => new ComplaintInteractionWriter.SourceOrderReference(
                        x.Source.OrderId,
                        x.Source.OrderExternalId,
                        x.Source.CustomerNameSnapshot))
                    .ToArray(),
                employeeId,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            if (command.Files.Count > 0)
            {
                uploadedAttachments = await _attachmentService.UploadListAsync(
                    report.AttachmentCollectionId,
                    AttachmentSlot.Complaint,
                    command.Files,
                    employeeId,
                    cancellationToken);
                if (uploadedAttachments.Count != command.Files.Count)
                {
                    return await FailAndRollbackAsync(
                        uploadedAttachments,
                        transaction,
                        "Số tệp complaint được lưu không khớp số tệp gửi lên.",
                        CancellationToken.None);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<ComplaintReportResultDto>.Ok(new ComplaintReportResultDto
            {
                ComplaintReportId = report.ComplaintReportId,
                ExternalId = report.ExternalId,
                AttachmentCollectionId = report.AttachmentCollectionId,
                Status = report.Status.ToString(),
                RequestedResolutionType = report.RequestedResolutionType?.ToString(),
                RequestedReplacementDeliveryDate = report.RequestedReplacementDeliveryDate,
                ResolutionType = null
            });
        }
        catch (OperationCanceledException)
        {
            await FailAndRollbackAsync(
                uploadedAttachments,
                transaction,
                "Thao tác đã bị hủy.",
                CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không thể tạo complaint report.");
            return await FailAndRollbackAsync(
                uploadedAttachments,
                transaction,
                "Không thể tạo complaint report; toàn bộ dữ liệu đã được rollback.",
                CancellationToken.None);
        }
    }

    private async Task<OperationResult<ComplaintReportResultDto>> FailAndRollbackAsync(
        IReadOnlyList<AttachmentDto> uploadedAttachments,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        string message,
        CancellationToken cancellationToken)
    {
        var attachmentIds = uploadedAttachments.Select(x => x.AttachmentId).ToArray();
        var paths = attachmentIds.Length == 0
            ? Array.Empty<string>()
            : await _dbContext.AttachmentModels
                .AsNoTracking()
                .Where(x => attachmentIds.Contains(x.AttachmentId))
                .Select(x => x.StoragePath)
                .ToArrayAsync(CancellationToken.None);

        foreach (var path in paths)
        {
            try
            {
                await _fileStorage.DeleteAsync(path, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Không thể cleanup file complaint {StoragePath}.", path);
            }
        }

        await transaction.RollbackAsync(cancellationToken);
        return OperationResult<ComplaintReportResultDto>.Fail(message);
    }

    private static string BuildInteractionContent(ComplaintReport report)
        => string.Join(
            Environment.NewLine,
            new[] { report.Summary, report.NonConformityDescription }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
