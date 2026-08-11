using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.Resubmit;

internal sealed class ResubmitComplaintReportCommandHandler
    : IRequestHandler<ResubmitComplaintReportCommand, OperationResult>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ComplaintEventLogWriter _eventLogWriter;

    public ResubmitComplaintReportCommandHandler(
        IComplaintReportDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        ComplaintEventLogWriter eventLogWriter)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _clock = clock;
        _eventLogWriter = eventLogWriter;
    }

    public async Task<OperationResult> Handle(
        ResubmitComplaintReportCommand command,
        CancellationToken cancellationToken)
    {
        if (!ComplaintAuthorizationRules.CanCreate(_currentUser))
        {
            return OperationResult.Fail("Bạn không có quyền gửi lại complaint.");
        }

        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        var report = await _dbContext.ComplaintReports
            .Include(x => x.ComplaintReportLines)
            .FirstOrDefaultAsync(x => x.ComplaintReportId == command.ComplaintReportId &&
                x.CompanyId == companyId && x.IsActive, cancellationToken);
        if (report is null)
        {
            return OperationResult.Fail("Không tìm thấy complaint trong công ty hiện tại.");
        }

        if (report.Status != ComplaintReportStatus.Draft || report.CreatedBy != employeeId)
        {
            return OperationResult.Fail("Chỉ người tạo mới được gửi lại complaint Draft.");
        }

        if (!report.ComplaintReportLines.Any(x => x.IsActive))
        {
            return OperationResult.Fail("Complaint phải có ít nhất một dòng active trước khi gửi lại.");
        }

        var now = _clock.Now;
        report.Status = ComplaintReportStatus.Submitted;
        report.UpdatedDate = now;
        report.UpdatedBy = employeeId;
        await _eventLogWriter.AddAsync(report, employeeId, "Sale resubmitted complaint after correction.", cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Đã gửi lại complaint để duyệt ban đầu.");
    }
}
