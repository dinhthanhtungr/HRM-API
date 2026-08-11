using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.FinalDecision;

internal sealed class FinalComplaintDecisionCommandHandler
    : IRequestHandler<FinalComplaintDecisionCommand, OperationResult>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ComplaintInteractionWriter _interactionWriter;
    private readonly ComplaintEventLogWriter _eventLogWriter;
    private readonly ComplaintDecisionNotificationService _notificationService;
    private readonly ComplaintReportAccessService _accessService;

    public FinalComplaintDecisionCommandHandler(
        IComplaintReportDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        ComplaintInteractionWriter interactionWriter,
        ComplaintEventLogWriter eventLogWriter,
        ComplaintDecisionNotificationService notificationService,
        ComplaintReportAccessService accessService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _clock = clock;
        _interactionWriter = interactionWriter;
        _eventLogWriter = eventLogWriter;
        _notificationService = notificationService;
        _accessService = accessService;
    }

    public async Task<OperationResult> Handle(
        FinalComplaintDecisionCommand command,
        CancellationToken cancellationToken)
    {
        if (!ComplaintAuthorizationRules.CanFinalApprove(_currentUser))
        {
            return OperationResult.Fail("Bạn không có quyền duyệt cuối complaint.");
        }

        if (!ComplaintDecisionRules.IsFinalDecisionSupported(command.Request.Decision))
        {
            return OperationResult.Fail("Duyệt cuối chỉ chấp nhận Approved hoặc Returned.");
        }

        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var report = await _dbContext.ComplaintReports
            .Include(x => x.ComplaintReportLines)
                .ThenInclude(x => x.SourceMerchandiseOrderDetail)
                    .ThenInclude(x => x.MerchandiseOrder)
            .Include(x => x.CapaActions)
            .FirstOrDefaultAsync(x => x.ComplaintReportId == command.ComplaintReportId &&
                x.CompanyId == companyId && x.IsActive, cancellationToken);
        if (report is null)
        {
            return OperationResult.Fail("Không tìm thấy complaint trong công ty hiện tại.");
        }


        if (!await _accessService.CanAccessCustomerAsync(report.CustomerId, cancellationToken))
        {
            return OperationResult.Fail(
                "Complaint report was not found or is outside your visibility scope.");
        }

        if (!ComplaintDecisionRules.CanMakeFinalDecision(report.Status))
        {
            return OperationResult.Fail("Chỉ complaint PendingFinalApproval mới được duyệt cuối.");
        }

        var actorName = await _dbContext.Employees.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.CompanyId == companyId && x.IsActive)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(actorName))
        {
            return OperationResult.Fail("Không tìm thấy người duyệt trong công ty hiện tại.");
        }

        var now = _clock.Now;
        report.Status = command.Request.Decision == ComplaintApprovalDecision.Approved
            ? ComplaintReportStatus.Closed
            : ComplaintReportStatus.ActionInProgress;
        report.CompletedAt = command.Request.Decision == ComplaintApprovalDecision.Approved ? now : null;
        report.CompletedBy = command.Request.Decision == ComplaintApprovalDecision.Approved ? employeeId : null;
        report.UpdatedDate = now;
        report.UpdatedBy = employeeId;
        await _dbContext.ComplaintReportApprovals.AddAsync(new ComplaintReportApproval
        {
            ComplaintReportApprovalId = Guid.CreateVersion7(),
            ComplaintReportId = report.ComplaintReportId,
            Stage = ComplaintApprovalStage.FinalHodApproval,
            Decision = command.Request.Decision,
            ActorId = employeeId,
            ActorNameSnapshot = actorName.Trim(),
            DecidedAt = now,
            Comment = Normalize(command.Request.Comment),
            IsActive = true,
            CreatedDate = now,
            CreatedBy = employeeId
        }, cancellationToken);

        var sourceOrders = report.ComplaintReportLines.Where(x => x.IsActive)
            .Select(x => x.SourceMerchandiseOrderDetail.MerchandiseOrder)
            .DistinctBy(x => x.MerchandiseOrderId)
            .Select(x => new ComplaintInteractionWriter.SourceOrderReference(
                x.MerchandiseOrderId, x.ExternalId, x.CustomerNameSnapshot))
            .ToArray();
        await _interactionWriter.AddAsync(
            report,
            $"Final decision: {command.Request.Decision}. Status: {report.Status}. {Normalize(command.Request.Comment)}",
            employeeId, now, sourceOrders, report.CreatedBy, cancellationToken);
        await _eventLogWriter.AddAsync(
            report, employeeId, $"Final decision: {command.Request.Decision}.", cancellationToken);
        await _notificationService.PublishFinalAsync(
            report,
            command.Request.Decision,
            employeeId,
            report.CapaActions.Where(x => x.IsActive).Select(x => x.PersonInChargeId)
                .Append(report.EffectivenessPersonInChargeId),
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok(command.Request.Decision == ComplaintApprovalDecision.Approved
            ? "Đã duyệt cuối và đóng complaint."
            : "Đã trả complaint về bước thực hiện CAPA.");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
