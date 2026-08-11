using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.RequestVerification;

internal sealed class RequestComplaintVerificationCommandHandler
    : IRequestHandler<RequestComplaintVerificationCommand, OperationResult>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ComplaintEventLogWriter _eventLogWriter;
    private readonly ComplaintReportAccessService _accessService;

    public RequestComplaintVerificationCommandHandler(
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
        RequestComplaintVerificationCommand command,
        CancellationToken cancellationToken)
    {
        if (!ComplaintAuthorizationRules.CanInvestigate(_currentUser))
        {
            return OperationResult.Fail("Bạn không có quyền gửi yêu cầu xác minh CAPA.");
        }

        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
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

        if (!ComplaintWorkflowRules.CanRequestVerification(report.Status))
        {
            return OperationResult.Fail("Chỉ được yêu cầu xác minh khi CAPA đang thực hiện.");
        }

        var activeActions = report.CapaActions.Where(x => x.IsActive).ToList();
        if (activeActions.Count == 0 || activeActions.Any(x =>
            !x.CompletedAt.HasValue || string.IsNullOrWhiteSpace(x.Result)))
        {
            return OperationResult.Fail("Tất cả hành động CAPA phải có kết quả và thời điểm hoàn thành.");
        }

        var now = _clock.Now;
        report.Status = ComplaintReportStatus.PendingVerification;
        report.UpdatedDate = now;
        report.UpdatedBy = employeeId;
        await _eventLogWriter.AddAsync(report, employeeId, "Gửi CAPA sang bước xác minh hiệu lực.", cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Đã gửi yêu cầu xác minh hiệu lực CAPA.");
    }
}
