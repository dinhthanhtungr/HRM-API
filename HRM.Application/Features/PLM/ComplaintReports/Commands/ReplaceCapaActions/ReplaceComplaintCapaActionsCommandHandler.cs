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

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.ReplaceCapaActions;

internal sealed class ReplaceComplaintCapaActionsCommandHandler
    : IRequestHandler<ReplaceComplaintCapaActionsCommand, OperationResult>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ComplaintEventLogWriter _eventLogWriter;
    private readonly ComplaintReportAccessService _accessService;

    public ReplaceComplaintCapaActionsCommandHandler(
        IComplaintReportDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        ComplaintEventLogWriter eventLogWriter,
        ComplaintReportAccessService accessService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _clock = clock;
        _eventLogWriter = eventLogWriter;
        _accessService = accessService;
    }

    public async Task<OperationResult> Handle(
        ReplaceComplaintCapaActionsCommand command,
        CancellationToken cancellationToken)
    {
        if (!ComplaintAuthorizationRules.CanManageActions(_currentUser))
        {
            return OperationResult.Fail("Bạn không có quyền quản lý hành động CAPA.");
        }

        var actions = command.Request.Immediate
            .Select(x => (Request: x, Type: ComplaintCapaActionType.Immediate))
            .Concat(command.Request.CorrectivePreventive.Select(x =>
                (Request: x, Type: ComplaintCapaActionType.CorrectivePreventive)))
            .ToList();
        var error = Validate(actions);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var report = await _dbContext.ComplaintReports
            .Include(x => x.CapaActions)
            .FirstOrDefaultAsync(x => x.ComplaintReportId == command.ComplaintReportId &&
                x.CompanyId == companyId && x.IsActive, cancellationToken);
        if (report is null)
        {
            return OperationResult.Fail("Không tìm thấy khiếu nại trong công ty hiện tại.");
        }

        if (!await _accessService.CanAccessCustomerAsync(report.CustomerId, cancellationToken))
        {
            return OperationResult.Fail(
                "Complaint report was not found or is outside your visibility scope.");
        }

        if (!ComplaintWorkflowRules.CanReplaceActions(report.Status))
        {
            return OperationResult.Fail("Chỉ được thay CAPA ở trạng thái Investigating hoặc ActionInProgress.");
        }

        var assigneeIds = actions.Select(x => x.Request.PersonInChargeId).Distinct().ToArray();
        var assignees = await _dbContext.Employees.AsNoTracking()
            .Where(x => assigneeIds.Contains(x.EmployeeId) && x.CompanyId == companyId && x.IsActive)
            .Select(x => new { x.EmployeeId, x.FullName })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, cancellationToken);
        if (assignees.Count != assigneeIds.Length)
        {
            return OperationResult.Fail("Có người phụ trách không tồn tại hoặc không thuộc công ty hiện tại.");
        }

        var now = _clock.Now;
        foreach (var oldAction in report.CapaActions.Where(x => x.IsActive))
        {
            oldAction.IsActive = false;
            oldAction.UpdatedDate = now;
            oldAction.UpdatedBy = employeeId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        foreach (var item in actions)
        {
            report.CapaActions.Add(new ComplaintCapaAction
            {
                ComplaintCapaActionId = Guid.CreateVersion7(),
                ComplaintReportId = report.ComplaintReportId,
                ActionType = item.Type,
                SortOrder = item.Request.SortOrder,
                Content = item.Request.Content!.Trim(),
                PersonInChargeId = item.Request.PersonInChargeId,
                PersonInChargeNameSnapshot = assignees[item.Request.PersonInChargeId].Trim(),
                Deadline = item.Request.Deadline,
                IsActive = true,
                CreatedDate = now,
                CreatedBy = employeeId,
                UpdatedDate = now,
                UpdatedBy = employeeId
            });
        }

        report.Status = ComplaintReportStatus.ActionInProgress;
        report.UpdatedDate = now;
        report.UpdatedBy = employeeId;
        await _eventLogWriter.AddAsync(report, employeeId, $"Thay danh sách CAPA gồm {actions.Count} hành động.", cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("Đã cập nhật danh sách hành động CAPA.");
    }

    private static string? Validate(
        IReadOnlyCollection<(ComplaintCapaActionRequest Request, ComplaintCapaActionType Type)> actions)
    {
        if (actions.Count == 0 || actions.Count > 100)
        {
            return "Danh sách CAPA phải có từ 1 đến 100 hành động.";
        }

        if (actions.Any(x => string.IsNullOrWhiteSpace(x.Request.Content) ||
            x.Request.Content.Trim().Length > 4000 ||
            x.Request.PersonInChargeId == Guid.Empty || x.Request.SortOrder < 0))
        {
            return "Nội dung, người phụ trách hoặc thứ tự CAPA không hợp lệ.";
        }

        if (actions.GroupBy(x => new { x.Type, x.Request.SortOrder }).Any(x => x.Count() > 1))
        {
            return "SortOrder phải duy nhất trong từng loại hành động CAPA.";
        }

        return null;
    }
}
