using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordSampleRequestSampleTrialCustomerFeedback;

internal sealed class RecordSampleRequestSampleTrialCustomerFeedbackCommandHandler
    : IRequestHandler<RecordSampleRequestSampleTrialCustomerFeedbackCommand, OperationResult<Guid>>
{
    private const int MaxCustomerReplyStatusLength = 100;
    private const int MaxCustomerReplyNoteLength = 5000;

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISender _sender;

    public RecordSampleRequestSampleTrialCustomerFeedbackCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
        _sender = sender;
    }

    public async Task<OperationResult<Guid>> Handle(
        RecordSampleRequestSampleTrialCustomerFeedbackCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("SampleRequestId is invalid.");
        }

        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaSelectors))
        {
            return OperationResult<Guid>.Fail("You are not allowed to record customer feedback for sample trials.");
        }

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("Current employee is invalid.");
        }

        if (!IsAllowedFeedbackStatus(request.Status))
        {
            return OperationResult<Guid>.Fail("Status must be WaitingCustomerFeedback, Approved, Failed or Cancelled.");
        }

        var replyStatus = TrimToNull(request.CustomerReplyStatus);
        var replyNote = TrimToNull(request.CustomerReplyNote);
        if (replyStatus is { Length: > MaxCustomerReplyStatusLength })
        {
            return OperationResult<Guid>.Fail($"CustomerReplyStatus cannot exceed {MaxCustomerReplyStatusLength} characters.");
        }

        if (replyNote is { Length: > MaxCustomerReplyNoteLength })
        {
            return OperationResult<Guid>.Fail($"CustomerReplyNote cannot exceed {MaxCustomerReplyNoteLength} characters.");
        }

        if (request.Status is SampleTrialStatus.Approved or SampleTrialStatus.Failed or SampleTrialStatus.Cancelled &&
            string.IsNullOrWhiteSpace(replyStatus))
        {
            return OperationResult<Guid>.Fail("CustomerReplyStatus is required for final customer feedback.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleSampleRequests = _visibilityService.ApplySampleRequestVisibility(
            _dbContext.SampleRequests.AsQueryable(),
            _dbContext.Customers.AsNoTracking(),
            scope);

        var sampleRequest = await visibleSampleRequests
            .FirstOrDefaultAsync(x => x.SampleRequestId == request.SampleRequestId, cancellationToken);
        if (sampleRequest is null)
        {
            return OperationResult<Guid>.Fail("Sample request was not found or is outside your scope.");
        }

        var trial = await _dbContext.SampleRequestSampleTrials
            .Include(x => x.Formula)
            .Where(x =>
                x.SampleRequestId == sampleRequest.SampleRequestId &&
                x.IsActive &&
                (x.Status == SampleTrialStatus.SampleSent ||
                 x.Status == SampleTrialStatus.WaitingCustomerFeedback ||
                 x.Status == SampleTrialStatus.PriceQuote))
            .OrderByDescending(x => x.TrialNo)
            .FirstOrDefaultAsync(cancellationToken);
        if (trial is null)
        {
            return OperationResult<Guid>.Fail("No active sample trial is waiting for customer feedback.");
        }

        if (request.ExpectedUpdatedDate.HasValue && trial.UpdatedDate != request.ExpectedUpdatedDate.Value)
        {
            return OperationResult<Guid>.Fail("Sample trial was changed by another user. Reload before saving.");
        }

        if (!CanRecordFromCurrentStatus(trial.Status, request.Status))
        {
            return OperationResult<Guid>.Fail("Customer feedback can only be recorded for a sample that is awaiting customer feedback.");
        }

        if (request.Status == SampleTrialStatus.Approved)
        {
            var approvalValidationError = SampleRequestSampleTrialApprovalRules.Validate(
                trial,
                trial.FormulaId ?? Guid.Empty);
            if (approvalValidationError is not null)
            {
                return OperationResult<Guid>.Fail(approvalValidationError);
            }
        }

        var now = _dateTimeProvider.Now;
        var replyDate = request.CustomerReplyDate ?? now;
        trial.Status = request.Status;
        trial.CustomerReplyStatus = replyStatus;
        trial.CustomerReplyDate = replyDate;
        trial.CustomerReplyByEmployeeId = employeeId;
        trial.CustomerReplyNote = replyNote;
        trial.UpdatedBy = employeeId;
        trial.UpdatedDate = now;

        var formulaExternalId = trial.Formula?.ExternalId ?? string.Empty;
        if (request.Status == SampleTrialStatus.Approved)
        {
            var productFormulas = await _dbContext.Formulas
                .Where(x =>
                    x.ProductId == sampleRequest.ProductId &&
                    x.CompanyId == scope.CompanyId &&
                    x.IsActive)
                .ToListAsync(cancellationToken);

            SampleRequestSampleTrialApprovalRules.ApplyApproved(
                sampleRequest,
                trial,
                productFormulas,
                employeeId,
                now,
                replyStatus!,
                replyNote,
                replyDate);
        }
        else if (request.Status == SampleTrialStatus.Failed)
        {
            SampleRequestStatusTransitionRules.MarkCustomerFailed(sampleRequest);
        }
        else if (request.Status == SampleTrialStatus.Cancelled)
        {
            SampleRequestStatusTransitionRules.MarkCustomerCancelled(sampleRequest);
        }

        sampleRequest.UpdatedBy = employeeId;
        sampleRequest.UpdatedDate = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var messageResult = await SendFeedbackMessageAsync(
            sampleRequest.SampleRequestId,
            sampleRequest.ExternalId,
            trial.TrialNo,
            formulaExternalId,
            request.Status,
            replyStatus,
            replyNote,
            cancellationToken);
        if (!messageResult.Success)
        {
            return OperationResult<Guid>.Ok(
                trial.SampleRequestSampleTrialId,
                $"Recorded customer feedback successfully, but could not send notification: {messageResult.Message}");
        }

        return OperationResult<Guid>.Ok(trial.SampleRequestSampleTrialId, "Recorded customer feedback successfully.");
    }

    private Task<OperationResult<SendInternalMessageResultDto>> SendFeedbackMessageAsync(
        Guid sampleRequestId,
        string sampleRequestExternalId,
        int trialNo,
        string formulaExternalId,
        SampleTrialStatus status,
        string? customerReplyStatus,
        string? customerReplyNote,
        CancellationToken cancellationToken)
    {
        var outcome = status switch
        {
            SampleTrialStatus.Approved => "Khach hang da chap nhan mau. Cong thuc da duoc hoan thanh.",
            SampleTrialStatus.Failed => "Khach hang chua dat mau. Lab vui long phat trien va gui lai mau.",
            SampleTrialStatus.Cancelled => "Khach hang da dung/tu choi yeu cau mau.",
            _ => "Sale da ghi nhan phan hoi cua khach hang dang cho xu ly."
        };

        var note = string.IsNullOrWhiteSpace(customerReplyNote) ? string.Empty : $" Ghi chu: {customerReplyNote}";
        return _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = $"Phan hoi khach hang cho yeu cau phoi mau {sampleRequestExternalId}, lan thu {trialNo}. Cong thuc: {formulaExternalId}. Trang thai: {customerReplyStatus}. {outcome}{note}",
            TopicOverride = TopicNotifications.SampleRequestCustomerFeedbackRecorded,
            TitleOverride = "Phan hoi khach hang ve mau da gui"
        }, cancellationToken);
    }

    private static bool IsAllowedFeedbackStatus(SampleTrialStatus status)
        => status is SampleTrialStatus.WaitingCustomerFeedback or
            SampleTrialStatus.Approved or
            SampleTrialStatus.Failed or
            SampleTrialStatus.Cancelled;

    private static bool CanRecordFromCurrentStatus(SampleTrialStatus currentStatus, SampleTrialStatus requestedStatus)
    {
        if (currentStatus is SampleTrialStatus.SampleSent or SampleTrialStatus.PriceQuote)
        {
            return true;
        }

        return currentStatus == SampleTrialStatus.WaitingCustomerFeedback &&
            requestedStatus is SampleTrialStatus.Approved or SampleTrialStatus.Failed or SampleTrialStatus.Cancelled;
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
