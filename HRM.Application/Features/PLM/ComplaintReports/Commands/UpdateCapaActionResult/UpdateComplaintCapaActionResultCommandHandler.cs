using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateCapaActionResult;

internal sealed class UpdateComplaintCapaActionResultCommandHandler
    : IRequestHandler<UpdateComplaintCapaActionResultCommand, OperationResult>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ComplaintEventLogWriter _eventLogWriter;
    private readonly ComplaintReportAccessService _accessService;

    public UpdateComplaintCapaActionResultCommandHandler(
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
        UpdateComplaintCapaActionResultCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Request.Result))
        {
            return OperationResult.Fail("Kết quả thực hiện là bắt buộc.");
        }

        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        var action = await _dbContext.ComplaintCapaActions
            .Include(x => x.ComplaintReport)
            .FirstOrDefaultAsync(x => x.ComplaintCapaActionId == command.ActionId &&
                x.ComplaintReportId == command.ComplaintReportId && x.IsActive &&
                x.ComplaintReport.CompanyId == companyId && x.ComplaintReport.IsActive,
                cancellationToken);
        if (action is null)
        {
            return OperationResult.Fail("Không tìm thấy hành động CAPA trong công ty hiện tại.");
        }

        if (!await _accessService.CanAccessCustomerAsync(
                action.ComplaintReport.CustomerId,
                cancellationToken))
        {
            return OperationResult.Fail(
                "Complaint report was not found or is outside your visibility scope.");
        }

        if (!ComplaintAuthorizationRules.CanUpdateAction(_currentUser, action.PersonInChargeId))
        {
            return OperationResult.Fail("Bạn không phải người phụ trách và không có quyền quản lý CAPA.");
        }

        if (!ComplaintWorkflowRules.CanUpdateActionResult(action.ComplaintReport.Status))
        {
            return OperationResult.Fail("Chỉ được cập nhật kết quả khi CAPA đang thực hiện.");
        }

        var now = _clock.Now;
        if (command.Request.CompletedAt > now)
        {
            return OperationResult.Fail("Thời điểm hoàn thành không được nằm trong tương lai.");
        }

        action.Result = command.Request.Result.Trim();
        action.CompletedAt = command.Request.CompletedAt;
        action.UpdatedDate = now;
        action.UpdatedBy = employeeId;
        action.ComplaintReport.UpdatedDate = now;
        action.ComplaintReport.UpdatedBy = employeeId;
        await _eventLogWriter.AddAsync(
            action.ComplaintReport,
            employeeId,
            command.Request.CompletedAt.HasValue
                ? $"Hoàn thành CAPA #{action.SortOrder}."
                : $"Cập nhật kết quả CAPA #{action.SortOrder}.",
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Đã cập nhật kết quả hành động CAPA.");
    }
}
