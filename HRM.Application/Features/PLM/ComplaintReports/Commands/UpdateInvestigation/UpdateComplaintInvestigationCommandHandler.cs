using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.ComplaintReports;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Enums.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateInvestigation;

internal sealed class UpdateComplaintInvestigationCommandHandler
    : IRequestHandler<UpdateComplaintInvestigationCommand, OperationResult>
{
    private readonly IComplaintReportDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ComplaintEventLogWriter _eventLogWriter;
    private readonly ComplaintReportAccessService _accessService;

    public UpdateComplaintInvestigationCommandHandler(
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
        UpdateComplaintInvestigationCommand command,
        CancellationToken cancellationToken)
    {
        if (!ComplaintAuthorizationRules.CanInvestigate(_currentUser))
        {
            return OperationResult.Fail("Bạn không có quyền cập nhật điều tra khiếu nại.");
        }

        var request = command.Request;
        if (!ComplaintWorkflowRules.AreStandardsValid(request.RelatedStandards) ||
            !ComplaintWorkflowRules.AreScopesValid(request.RelatedScopes))
        {
            return OperationResult.Fail("Tiêu chuẩn hoặc phạm vi liên quan không hợp lệ.");
        }

        if (request.RelatedStandards.HasFlag(ComplaintRelatedStandard.Other) &&
            string.IsNullOrWhiteSpace(request.OtherRelatedStandard))
        {
            return OperationResult.Fail("Phải mô tả tiêu chuẩn khác khi chọn Other.");
        }

        if (string.IsNullOrWhiteSpace(request.NonConformityDescription) ||
            string.IsNullOrWhiteSpace(request.RootCause))
        {
            return OperationResult.Fail("Mô tả sự không phù hợp và nguyên nhân gốc là bắt buộc.");
        }

        if (request.HasNewRisk == true && string.IsNullOrWhiteSpace(request.RiskReviewComment))
        {
            return OperationResult.Fail("Phải ghi nhận đánh giá khi phát hiện rủi ro mới.");
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

        if (!ComplaintWorkflowRules.CanEditInvestigation(report.Status))
        {
            return OperationResult.Fail("Chỉ được cập nhật điều tra ở trạng thái Investigating hoặc ActionInProgress.");
        }

        var partIds = new[] { request.IssuePartId, request.CausingPartId }
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var parts = partIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Parts.AsNoTracking()
                .Where(x => partIds.Contains(x.PartId))
                .ToDictionaryAsync(x => x.PartId, x => x.PartName, cancellationToken);
        if (parts.Count != partIds.Length)
        {
            return OperationResult.Fail("Bộ phận liên quan không tồn tại.");
        }

        var now = _clock.Now;
        report.IssuePartId = request.IssuePartId;
        report.IssuePartNameSnapshot = request.IssuePartId.HasValue
            ? parts[request.IssuePartId.Value].Trim()
            : null;
        report.RelatedStandards = request.RelatedStandards;
        report.OtherRelatedStandard = request.RelatedStandards.HasFlag(ComplaintRelatedStandard.Other)
            ? Normalize(request.OtherRelatedStandard)
            : null;
        report.RelatedScopes = request.RelatedScopes;
        report.DocumentRequirement = Normalize(request.DocumentRequirement);
        report.NonConformityDescription = request.NonConformityDescription.Trim();
        report.RootCause = request.RootCause.Trim();
        report.InterestedPartyComment = Normalize(request.InterestedPartyComment);
        report.CausingPartId = request.CausingPartId;
        report.CausingPartySnapshot = request.CausingPartId.HasValue
            ? parts[request.CausingPartId.Value].Trim()
            : Normalize(request.CausingParty);
        report.RiskReviewedAt = now;
        report.HasNewRisk = request.HasNewRisk;
        report.RiskReviewComment = Normalize(request.RiskReviewComment);
        report.UpdatedDate = now;
        report.UpdatedBy = employeeId;

        await _eventLogWriter.AddAsync(report, employeeId, "Cập nhật nội dung điều tra và đánh giá rủi ro.", cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Đã cập nhật nội dung điều tra khiếu nại.");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
