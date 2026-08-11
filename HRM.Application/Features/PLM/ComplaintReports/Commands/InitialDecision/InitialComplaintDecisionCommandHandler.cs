using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.InitialDecision;

internal sealed class InitialComplaintDecisionCommandHandler
    : IRequestHandler<InitialComplaintDecisionCommand, OperationResult<ComplaintReportResultDto>>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ComplaintHandlingOrderService _handlingOrderService;
    private readonly SaleOrderApprovalService _saleOrderApprovalService;
    private readonly ComplaintInteractionWriter _interactionWriter;
    private readonly ComplaintEventLogWriter _eventLogWriter;
    private readonly ComplaintDecisionNotificationService _notificationService;
    private readonly ComplaintReportAccessService _accessService;

    public InitialComplaintDecisionCommandHandler(
        IComplaintReportDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        ComplaintHandlingOrderService handlingOrderService,
        SaleOrderApprovalService saleOrderApprovalService,
        ComplaintInteractionWriter interactionWriter,
        ComplaintEventLogWriter eventLogWriter,
        ComplaintDecisionNotificationService notificationService,
        ComplaintReportAccessService accessService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _clock = clock;
        _handlingOrderService = handlingOrderService;
        _saleOrderApprovalService = saleOrderApprovalService;
        _interactionWriter = interactionWriter;
        _eventLogWriter = eventLogWriter;
        _notificationService = notificationService;
        _accessService = accessService;
    }

    public async Task<OperationResult<ComplaintReportResultDto>> Handle(
        InitialComplaintDecisionCommand command,
        CancellationToken cancellationToken)
    {
        if (!ComplaintAuthorizationRules.CanInitialApprove(_currentUser))
        {
            return OperationResult<ComplaintReportResultDto>.Fail("Bạn không có quyền duyệt ban đầu complaint.");
        }

        var request = command.Request;
        if (!ComplaintDecisionRules.IsInitialDecisionSupported(request.Decision))
        {
            return OperationResult<ComplaintReportResultDto>.Fail("Quyết định ban đầu không hợp lệ.");
        }

        if (request.Decision == ComplaintApprovalDecision.Approved &&
            (!request.ResolutionType.HasValue || !Enum.IsDefined(request.ResolutionType.Value)))
        {
            return OperationResult<ComplaintReportResultDto>.Fail("Approver phải chọn hướng xử lý cuối khi duyệt.");
        }

        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var report = await _dbContext.ComplaintReports
            .Include(x => x.ComplaintReportLines)
                .ThenInclude(x => x.SourceMerchandiseOrderDetail)
                    .ThenInclude(x => x.MerchandiseOrder)
            .FirstOrDefaultAsync(x => x.ComplaintReportId == command.ComplaintReportId &&
                x.CompanyId == companyId && x.IsActive, cancellationToken);
        if (report is null)
        {
            return OperationResult<ComplaintReportResultDto>.Fail("Không tìm thấy complaint trong công ty hiện tại.");
        }

        if (!await _accessService.CanAccessCustomerAsync(report.CustomerId, cancellationToken))
        {
            return OperationResult<ComplaintReportResultDto>.Fail(
                "Complaint report was not found or is outside your visibility scope.");
        }

        if (!ComplaintDecisionRules.CanMakeInitialDecision(report.Status))
        {
            return OperationResult<ComplaintReportResultDto>.Fail("Chỉ complaint Submitted mới được duyệt ban đầu.");
        }

        var actorName = await FindEmployeeNameAsync(employeeId, companyId, cancellationToken);
        if (actorName is null)
        {
            return OperationResult<ComplaintReportResultDto>.Fail("Không tìm thấy người duyệt trong công ty hiện tại.");
        }

        var activeLines = report.ComplaintReportLines.Where(x => x.IsActive).ToList();
        var isReplacement = request.Decision == ComplaintApprovalDecision.Approved &&
            request.ResolutionType == ComplaintResolutionType.ReplacementProduction;
        Dictionary<Guid, decimal?> quantities = new();
        if (isReplacement)
        {
            try
            {
                quantities = request.Lines.ToDictionary(
                    x => x.ComplaintReportLineId,
                    x => x.ApprovedReplacementQuantity);
            }
            catch (ArgumentException)
            {
                return OperationResult<ComplaintReportResultDto>.Fail(
                    "ComplaintReportLineId bị trùng trong quyết định.");
            }

            var quantityError = ComplaintDecisionRules.ValidateReplacementQuantities(activeLines, quantities);
            if (quantityError is not null)
            {
                return OperationResult<ComplaintReportResultDto>.Fail(quantityError);
            }
        }

        var now = _clock.Now;
        report.ResolutionType = request.Decision == ComplaintApprovalDecision.Approved
            ? request.ResolutionType
            : null;
        report.Status = request.Decision switch
        {
            ComplaintApprovalDecision.Approved => ComplaintReportStatus.Investigating,
            ComplaintApprovalDecision.Rejected => ComplaintReportStatus.Rejected,
            ComplaintApprovalDecision.Returned => ComplaintReportStatus.Draft,
            _ => report.Status
        };
        report.UpdatedDate = now;
        report.UpdatedBy = employeeId;
        foreach (var line in activeLines)
        {
            line.ApprovedReplacementQuantity = isReplacement
                ? quantities[line.ComplaintReportLineId]
                : null;
            line.UpdatedDate = now;
            line.UpdatedBy = employeeId;
        }

        await _dbContext.ComplaintReportApprovals.AddAsync(CreateApproval(
            report, ComplaintApprovalStage.InitialHodApproval, request.Decision,
            employeeId, actorName, request.Comment, now), cancellationToken);
        var sourceOrders = SourceOrders(activeLines);
        await _interactionWriter.AddAsync(
            report,
            $"Initial decision: {request.Decision}. Resolution: {report.ResolutionType?.ToString() ?? "None"}. {Normalize(request.Comment)}",
            employeeId, now, sourceOrders, report.CreatedBy, cancellationToken);
        await _eventLogWriter.AddAsync(
            report, employeeId, $"Initial decision: {request.Decision}.", cancellationToken);

        // Persist readiness inside the transaction so SaleOrderApprovalService can verify it using a DB query.
        await _dbContext.SaveChangesAsync(cancellationToken);
        MerchandiseOrder? handlingOrder = null;
        if (isReplacement)
        {
            var handlingResult = await _handlingOrderService.CreateAsync(
                report, employeeId, now, report.RequestedReplacementDeliveryDate, cancellationToken);
            if (!handlingResult.Success || handlingResult.Data is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult<ComplaintReportResultDto>.Fail(
                    handlingResult.Message ?? "Không thể tạo hoặc đồng bộ đơn sản xuất bù.");
            }

            handlingOrder = handlingResult.Data;
            var approvalResult = await _saleOrderApprovalService.ApproveAsync(
                handlingOrder, employeeId, now, cancellationToken);
            if (!approvalResult.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult<ComplaintReportResultDto>.Fail(
                    approvalResult.Message ?? "Không thể duyệt đơn sản xuất bù hoặc tạo MFG.");
            }

            await _interactionWriter.AddAsync(
                report,
                $"Replacement production approved. Handling order: {handlingOrder.ExternalId}. Total amount: 0.",
                employeeId, now, sourceOrders, report.CreatedBy, cancellationToken);
        }

        await _notificationService.PublishInitialAsync(
            report, request.Decision, employeeId, handlingOrder?.MerchandiseOrderId, cancellationToken);
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
            ResolutionType = report.ResolutionType?.ToString(),
            HandlingMerchandiseOrderId = handlingOrder?.MerchandiseOrderId,
            HandlingMerchandiseOrderExternalId = handlingOrder?.ExternalId
        }, "Đã ghi nhận quyết định ban đầu.");
    }

    private Task<string?> FindEmployeeNameAsync(Guid employeeId, Guid companyId, CancellationToken cancellationToken)
        => _dbContext.Employees.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.CompanyId == companyId && x.IsActive)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken);

    private static ComplaintReportApproval CreateApproval(
        ComplaintReport report, ComplaintApprovalStage stage, ComplaintApprovalDecision decision,
        Guid actorId, string actorName, string? comment, DateTime now)
        => new()
        {
            ComplaintReportApprovalId = Guid.CreateVersion7(),
            ComplaintReportId = report.ComplaintReportId,
            Stage = stage,
            Decision = decision,
            ActorId = actorId,
            ActorNameSnapshot = actorName.Trim(),
            DecidedAt = now,
            Comment = Normalize(comment),
            IsActive = true,
            CreatedDate = now,
            CreatedBy = actorId
        };

    private static IReadOnlyCollection<ComplaintInteractionWriter.SourceOrderReference> SourceOrders(
        IEnumerable<ComplaintReportLine> lines)
        => lines.Select(x => x.SourceMerchandiseOrderDetail.MerchandiseOrder)
            .DistinctBy(x => x.MerchandiseOrderId)
            .Select(x => new ComplaintInteractionWriter.SourceOrderReference(
                x.MerchandiseOrderId, x.ExternalId, x.CustomerNameSnapshot))
            .ToArray();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
