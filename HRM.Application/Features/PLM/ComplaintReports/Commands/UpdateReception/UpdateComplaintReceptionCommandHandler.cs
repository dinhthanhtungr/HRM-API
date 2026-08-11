using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateReception;

/// <summary>
/// Atomically replaces sale-owned reception lines and lots while a report is Draft. It never changes status,
/// final resolution, approvals, attachments, handling orders or manufacturing orders.
/// </summary>
internal sealed class UpdateComplaintReceptionCommandHandler
    : IRequestHandler<UpdateComplaintReceptionCommand, OperationResult<ComplaintReportResultDto>>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ComplaintReceptionResolver _receptionResolver;

    public UpdateComplaintReceptionCommandHandler(
        IComplaintReportDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ICustomerVisibilityService visibilityService,
        ComplaintReceptionResolver receptionResolver)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _visibilityService = visibilityService;
        _receptionResolver = receptionResolver;
    }

    public async Task<OperationResult<ComplaintReportResultDto>> Handle(
        UpdateComplaintReceptionCommand command,
        CancellationToken cancellationToken)
    {
        if (!ComplaintAuthorizationRules.CanCreate(_currentUser))
        {
            return OperationResult<ComplaintReportResultDto>.Fail("Bạn không có quyền sửa phần tiếp nhận khiếu nại.");
        }

        if (command.ComplaintReportId == Guid.Empty)
        {
            return OperationResult<ComplaintReportResultDto>.Fail("Complaint report không hợp lệ.");
        }

        var request = command.Request;
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
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var report = await _dbContext.ComplaintReports
            .Include(x => x.ComplaintReportLines)
                .ThenInclude(x => x.Lots)
            .FirstOrDefaultAsync(x =>
                x.ComplaintReportId == command.ComplaintReportId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);
        if (report is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<ComplaintReportResultDto>.Fail("Không tìm thấy complaint report.");
        }

        var canAccessCustomer = await _visibilityService.ApplyCustomerVisibility(
                _dbContext.Customers.AsNoTracking(),
                scope)
            .AnyAsync(x => x.CustomerId == report.CustomerId, cancellationToken);
        if (!canAccessCustomer)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<ComplaintReportResultDto>.Fail(
                "Bạn không có quyền truy cập khách hàng của complaint report.");
        }

        if (report.Status != ComplaintReportStatus.Draft)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<ComplaintReportResultDto>.Fail(
                "Chỉ có thể sửa phần tiếp nhận khi complaint report đang ở Draft.");
        }

        if (report.CreatedBy != employeeId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<ComplaintReportResultDto>.Fail(
                "Chỉ sale đã tạo complaint report mới được sửa phần tiếp nhận.");
        }

        var resolvedResult = await _receptionResolver.ResolveAsync(
            report.CustomerId,
            request.Lines,
            report.ComplaintReportId,
            cancellationToken);
        if (!resolvedResult.Success || resolvedResult.Data is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<ComplaintReportResultDto>.Fail(
                resolvedResult.Message ?? "Dữ liệu tiếp nhận khiếu nại không hợp lệ.");
        }

        var now = _dateTimeProvider.Now;
        foreach (var oldLine in report.ComplaintReportLines.Where(x => x.IsActive))
        {
            oldLine.IsActive = false;
            oldLine.UpdatedDate = now;
            oldLine.UpdatedBy = employeeId;
            foreach (var oldLot in oldLine.Lots.Where(x => x.IsActive))
            {
                oldLot.IsActive = false;
                oldLot.UpdatedDate = now;
                oldLot.UpdatedBy = employeeId;
            }
        }

        report.Summary = request.Summary!.Trim();
        report.NonConformityDescription = Normalize(request.NonConformityDescription);
        report.RequestedResolutionType = request.RequestedResolutionType;
        report.RequestedReplacementDeliveryDate = request.RequestedReplacementDeliveryDate;
        report.UpdatedDate = now;
        report.UpdatedBy = employeeId;

        // Flush soft-deactivation first so filtered unique indexes cannot conflict with replacement rows.
        await _dbContext.SaveChangesAsync(cancellationToken);
        foreach (var resolvedLine in resolvedResult.Data.Lines)
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
            ResolutionType = report.ResolutionType?.ToString()
        });
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
