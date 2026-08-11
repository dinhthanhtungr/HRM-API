using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateEffectiveness;

internal sealed class UpdateComplaintEffectivenessCommandHandler
    : IRequestHandler<UpdateComplaintEffectivenessCommand, OperationResult>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ComplaintEventLogWriter _eventLogWriter;
    private readonly ComplaintReportAccessService _accessService;

    public UpdateComplaintEffectivenessCommandHandler(
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
        UpdateComplaintEffectivenessCommand command,
        CancellationToken cancellationToken)
    {
        if (!ComplaintAuthorizationRules.CanVerify(_currentUser))
        {
            return OperationResult.Fail("Bạn không có quyền xác minh hiệu lực CAPA.");
        }

        var request = command.Request;
        if (request.PersonInChargeId == Guid.Empty || request.ReviewUntil == default ||
            !Enum.IsDefined(request.Conclusion))
        {
            return OperationResult.Fail("Thông tin xác minh hiệu lực không hợp lệ.");
        }

        if ((request.HasRecurrence || request.Conclusion == ComplaintEffectivenessConclusion.NotOk) &&
            string.IsNullOrWhiteSpace(request.Comment))
        {
            return OperationResult.Fail("Phải ghi chú khi CAPA tái diễn hoặc chưa đạt hiệu lực.");
        }

        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        var report = await _dbContext.ComplaintReports.FirstOrDefaultAsync(x =>
            x.ComplaintReportId == command.ComplaintReportId &&
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

        if (!ComplaintWorkflowRules.CanSaveEffectiveness(report.Status))
        {
            return OperationResult.Fail("Chỉ được xác minh hiệu lực ở trạng thái PendingVerification.");
        }

        var personName = await _dbContext.Employees.AsNoTracking()
            .Where(x => x.EmployeeId == request.PersonInChargeId &&
                x.CompanyId == companyId && x.IsActive)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(personName))
        {
            return OperationResult.Fail("Người phụ trách xác minh không thuộc công ty hiện tại.");
        }

        var now = _clock.Now;
        report.EffectivenessPersonInChargeId = request.PersonInChargeId;
        report.EffectivenessPersonInChargeNameSnapshot = personName.Trim();
        report.EffectivenessReviewUntil = request.ReviewUntil;
        report.HasRecurrence = request.HasRecurrence;
        report.EffectivenessConclusion = request.Conclusion;
        report.EffectivenessComment = Normalize(request.Comment);
        report.Status = ComplaintReportStatus.PendingFinalApproval;
        report.UpdatedDate = now;
        report.UpdatedBy = employeeId;
        await _eventLogWriter.AddAsync(report, employeeId, "Hoàn tất xác minh hiệu lực, chờ duyệt cuối.", cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Đã lưu xác minh hiệu lực và chuyển sang chờ duyệt cuối.");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
